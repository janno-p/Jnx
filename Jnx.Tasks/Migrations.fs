module Jnx.Tasks.Migrations

open System
open System.Collections.Generic
open System.Data
open System.Linq.Expressions
open System.Text
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Linq.RuntimeHelpers

type ColumnType =
    | Boolean
    | Decimal of int * int
    | Integer
    | Serial
    | SmallInt
    | String of int
    | Text
    | Timestamp

type ITableBuilder<'T> =
    abstract member AddColumn : Expr<'T -> 'a> -> ColumnType -> unit
    abstract member ForeignKey : Expr<'T -> 'F -> 'a> -> unit
    abstract member PrimaryKey : Expr<'T -> 'a> -> unit
    abstract member Unique : Expr<'T -> 'a> -> unit

type IMigrationBuilder<'T> =
    abstract member CreateTable : (ITableBuilder<'T> -> unit) -> unit
    abstract member DropTable : unit -> unit
    abstract member Populate : 'T list -> unit

type TableMeta<'T> () =
    let tableName = typeof<'T>.Name
    let columns = new Dictionary<string, (ColumnType * Type)>()
    let uniqueKeys = new List<string list>()
    let foreignKeys = new List<(string * string list * string list)>()
    let mutable primaryKeys = [ "id" ]

    let IsOption (tp : Type) = tp.IsGenericType && tp.GetGenericTypeDefinition() = typedefof<Option<_>>

    let GetColumnSpec (columnType, runtimeType) =
        let typeInfo = match columnType with
                       | Boolean -> "BOOLEAN"
                       | Decimal (precision, scale) -> sprintf "DECIMAL(%d,%d)" precision scale
                       | Integer -> "INTEGER"
                       | Serial -> "SERIAL"
                       | SmallInt -> "SMALLINT"
                       | String length -> sprintf "VARCHAR(%d)" length
                       | Text -> "TEXT"
                       | Timestamp -> "TIMESTAMP"
        let nullable = match IsOption runtimeType with
                       | true -> "NULL"
                       | _ -> "NOT NULL"
        sprintf "%s %s" typeInfo nullable

    override this.ToString () =
        let separator = "," + Environment.NewLine
        let sql = new StringBuilder()
        sql.AppendLine (sprintf @"CREATE TABLE ""%s"" (" tableName) |> ignore
        if primaryKeys.Length = 1 && primaryKeys.[0] = "id" && not (columns.ContainsKey "id") then
            columns.Add("id", (Serial, typeof<int>))
        let fullColumns = columns |> Seq.map (fun k -> sprintf @"""%s"" %s" k.Key (GetColumnSpec k.Value)) |> Seq.toArray
        let fieldPart = Some (String.Join(separator, fullColumns))
        let primaryKeyPart = match primaryKeys with
                             | [] -> None
                             | _ -> Some (sprintf @"CONSTRAINT ""pk_%s"" PRIMARY KEY (%s)" tableName (String.Join(", ", (primaryKeys |> List.map (fun x -> sprintf @"""%s""" x)))))
        let uniqueKeyStrings = uniqueKeys |> Seq.filter (fun x -> x |> List.length > 0)
                                          |> Seq.map (fun x -> sprintf @"CONSTRAINT ""uq_%s_%s"" UNIQUE (%s)" tableName (String.Join("_", x)) (String.Join(", ", (x |> List.map (fun x -> sprintf @"""%s""" x)))))
                                          |> Seq.toList
        let uniquePart = match uniqueKeyStrings with
                         | [] -> None
                         | _ -> Some (String.Join(separator, uniqueKeyStrings))

        let fkPart = match foreignKeys.Count with
                     | 0 -> None
                     | _ -> Some (String.Join(separator, (foreignKeys |> Seq.map (fun (n, x, y) -> sprintf @"CONSTRAINT ""fk_%s_%s"" FOREIGN KEY (%s) REFERENCES ""%s"" (%s)" tableName n (String.Join(", ", (x |> List.map (fun x1 -> sprintf @"""%s""" x1)))) n (String.Join(", ", (y |> List.map (fun x1 -> sprintf @"""%s""" x1))))))))
        sql.AppendLine (String.Join(separator, [ fieldPart; primaryKeyPart; uniquePart; fkPart ] |> List.choose (fun x -> x) |> List.toArray)) |> ignore
        sql.Append (")") |> ignore
        sql.ToString()
    interface ITableBuilder<'T> with
        member this.AddColumn expr columnType =
            match expr with
            | Lambda(param, body) ->
                match body with
                | PropertyGet(_, pi, _) -> columns.Add(pi.Name, (columnType, pi.PropertyType))
                | _ -> failwith "Unsupported expression!"
            | _ -> failwith "Unsupported expression!"
        member this.ForeignKey (expr : Expr<'T -> 'F -> 'a>) =
            match expr with
            | Lambda(param, body) ->
                match body with
                | Lambda(param, body) ->
                    match body with
                    | NewTuple(exprlist) ->
                        match exprlist with
                        | [t; fk] ->
                            match (t, fk) with
                            | (PropertyGet(_, tpi, _), PropertyGet(_, fkpi, _)) ->
                                foreignKeys.Add((typeof<'F>.Name, [tpi.Name], [fkpi.Name]))
                            | (NewTuple(tl), NewTuple(fkl)) ->
                                foreignKeys.Add((typeof<'F>.Name,
                                                 tl |> List.choose (fun e -> match e with | PropertyGet(_, pi, _) -> Some (pi.Name) | _ -> None),
                                                 fkl |> List.choose (fun e -> match e with | PropertyGet(_, pi, _) -> Some (pi.Name) | _ -> None)))
                            | _ -> failwith (sprintf "Unsupported expression: (%O, %O) !" t fk)
                        | _ -> failwith (sprintf "Unsupported expression: %O!" exprlist)
                    | _ -> failwith (sprintf "Unsupported expression: %O!" body)
                | _ -> failwith (sprintf "Unsupported expression: %O!" body)
            | _ -> failwith (sprintf "Unsupported expression: %O!" expr)
        member this.PrimaryKey expr =
            match expr with
            | Lambda(param, body) ->
                match body with
                | PropertyGet(_, pi, _) -> primaryKeys <- [ pi.Name ]
                | NewTuple(exprlist) -> primaryKeys <- exprlist
                                                       |> List.choose (fun e -> match e with
                                                                                | PropertyGet(_, pi, _) -> Some (pi.Name)
                                                                                | _ -> None)
                | _ -> failwith "Unsupported expression!"
            | _ -> failwith "Unsupported expression!"
        member this.Unique expr =
            match expr with
            | Lambda(param, body) ->
                match body with
                | PropertyGet(_, pi, _) -> uniqueKeys.Add([ pi.Name ])
                | NewTuple(exprlist) -> uniqueKeys.Add(exprlist
                                                       |> List.choose (fun e -> match e with
                                                                                | PropertyGet(_, pi, _) -> Some (pi.Name)
                                                                                | _ -> None))
                | _ -> failwith "Unsupported expression!"
            | _ -> failwith "Unsupported expression!"

let Migrate (connection : IDbConnection) (run : IMigrationBuilder<'T> -> unit) =
    let sb = StringBuilder ()
    let (~~) (text : string) = sb.Append(text) |> ignore

    let executeCommand sql =
        use command = connection.CreateCommand()
        command.CommandText <- sql
        command.ExecuteNonQuery() |> ignore

    let migrationBuilder =
        { new IMigrationBuilder<'T> with
            member this.CreateTable (buildTable : ITableBuilder<'T> -> unit) =
                let table = TableMeta<'T> ()
                buildTable table
                executeCommand (table.ToString())
            member this.DropTable () =
                executeCommand (sprintf @"DROP TABLE ""%s""" typeof<'T>.Name)
            member this.Populate (data : 'T list) =
                sb.Clear() |> ignore
                let tp = typeof<'T>
                let tableName = tp.Name
                let columns = tp.GetProperties() |> Array.map (fun pi -> (pi.Name, pi)) |> Array.toList
                ~~(sprintf @"INSERT INTO ""%s"" (%s) VALUES " tableName (String.Join(", ", columns |> List.map (fun (name, _) -> sprintf @"""%s""" name))))
                let FormatType o (pi : Reflection.PropertyInfo) =
                    match pi.PropertyType.FullName with
                    | "System.String" -> sprintf @"'%s'" (pi.GetValue(o, [||]) :?> string)
                    | _ -> failwith (sprintf "Unmapped type %s" pi.PropertyType.FullName)
                ~~(String.Join(", ", data |> List.map (fun row -> sprintf "(%s)" (String.Join(", ", columns |> List.map (fun (_, pi) -> FormatType row pi))))))
                executeCommand (sb.ToString()) }

    run migrationBuilder
