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
    | Serial
    | String of int
    | Text

type Nullable =
    | Null
    | NotNull

type ITableBuilder<'T> =
    abstract member AddColumn : Expr<'T -> 'a> -> ColumnType -> Nullable -> unit
    abstract member PrimaryKey : Expr<'T -> 'a> -> unit
    abstract member Unique : Expr<'T -> 'a> -> unit

type IMigrationBuilder<'T> =
    abstract member CreateTable : (ITableBuilder<'T> -> unit) -> unit
    abstract member DropTable : unit -> unit
    abstract member Populate : 'T list -> unit

type TableMeta<'T> () =
    let tableName = typeof<'T>.Name
    let columns = new Dictionary<string, (ColumnType * Nullable)>()
    let uniqueKeys = new List<string list>()
    let mutable primaryKeys = [ "id" ]
    let GetColumnSpec (columnType, nullable) =
        let typeInfo = match columnType with
                       | Serial -> "SERIAL"
                       | String length -> sprintf "VARCHAR(%d)" length
                       | Text -> "TEXT"
        let nullableInfo = match nullable with
                           | Null -> "NULL"
                           | NotNull -> "NOT NULL"
        sprintf "%s %s" typeInfo nullableInfo
    override this.ToString () =
        let separator = "," + Environment.NewLine
        let sql = new StringBuilder()
        sql.AppendLine (sprintf @"CREATE TABLE ""%s"" (" tableName) |> ignore
        if primaryKeys.Length = 1 && primaryKeys.[0] = "id" && not (columns.ContainsKey "id") then
            columns.Add("id", (Serial, NotNull))
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
        sql.AppendLine (String.Join(separator, [ fieldPart; primaryKeyPart; uniquePart ] |> List.choose (fun x -> x) |> List.toArray)) |> ignore
        sql.Append (")") |> ignore
        sql.ToString()
    interface ITableBuilder<'T> with
        member this.AddColumn expr columnType nullable =
            match expr with
            | Lambda(param, body) ->
                match body with
                | PropertyGet(_, pi, _) -> columns.Add(pi.Name, (columnType, nullable))
                | _ -> failwith "Unsupported expression!"
            | _ -> failwith "Unsupported expression!"
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
