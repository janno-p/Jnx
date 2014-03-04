open System
open System.Text

module Migration =
    type ColumnOptions = {
        Name : string
        SampleValue : obj
    }

    type Name = string
    type ColumnName = string

    type Constraint =
        | PrimaryKey of Name * ColumnName list
        | Unique of Name * ColumnName list

    type DatabaseType<'T> =
        | Nullable of 'T
        | NotNullable of 'T

    type MigrationCommand = {
        CreateTable : bool
        TableName : string
        Columns : ColumnOptions list
        Constraints : Constraint list
    }

    let getTypeMapping (value : obj) =
        match value with
        | :? string -> "TEXT"
        | :? int -> "INT"
        | :? int64 -> "LONG"
        | :? decimal -> "NUMBER"
        | _ -> failwith "Unmapped type"

    let isNullable value =
        let optionType = typedefof<option<_>>
        if value = null then
            failwith "No nulls allowed"
        let valueType = value.GetType()
        valueType.IsGenericType && valueType.GetGenericTypeDefinition() = optionType

    let getDatabaseType value =
        if isNullable value then
            let valueProperty = value.GetType().GetProperty("Value")
            Nullable (getTypeMapping (valueProperty.GetValue(value, [| |])))
        else NotNullable (getTypeMapping value)

    let defaultMigrationCommand = {
        CreateTable = false
        TableName = ""
        Columns = []
        Constraints = []
    }

    let run (x : unit -> MigrationCommand) =
        let command = x()
        if command.CreateTable = true then
            let builder = new StringBuilder()
            builder.AppendLine(sprintf "CREATE TABLE %s (" command.TableName) |> ignore
            seq {
                for col in command.Columns do
                    match getDatabaseType(col.SampleValue) with
                    | Nullable typeName -> yield sprintf "%s %s" col.Name typeName
                    | NotNullable typeName -> yield sprintf "%s %s NOT NULL" col.Name typeName
                for cons in command.Constraints do
                    match cons with
                    | PrimaryKey(name, columns) -> yield sprintf "CONSTRAINT %s PRIMARY KEY (%s)" name (columns |> Seq.reduce (fun acc el -> sprintf "%s,%s" acc el))
                    | Unique(name, columns) -> yield sprintf "CONSTRAINT %s UNIQUE (%s)" name (columns |> Seq.reduce (fun acc el -> sprintf "%s,%s" acc el))
            } |> String.concat (sprintf ",%s" Environment.NewLine)
            |> builder.AppendLine
            |> ignore
            builder.AppendLine(")") |> ignore
            builder.ToString()
        else
            "<empty>"

    let create_table x name = (fun () ->
        { x() with CreateTable = true
                   TableName = name })

    let add_column x name tp = (fun () ->
        let command = x()
        { command with Columns = List.append command.Columns [{ Name = name; SampleValue = tp }] }
    )

    let complete (x : MigrationCommand) = (fun () -> x)

    let bind x rest = (rest x)

    let initialize = (fun () -> defaultMigrationCommand)

    let primaryKey x name tp = (fun () ->
        let command = x()
        { command with Constraints = List.append command.Constraints [ PrimaryKey(name, tp) ] }
    )

    let unique x name columns = (fun () ->
        let command = x()
        { command with Constraints = List.append command.Constraints [ Unique(name, columns) ] }
    )

    let Execute sql =
        printfn "%s" sql

type MigrationBuilder() =
    member this.Run(x) = Migration.run x
    member this.Bind(x, rest) = Migration.bind x rest
    member this.Return(x) = Migration.complete x
    member this.Yield(()) = (fun () -> Migration.defaultMigrationCommand)
    [<CustomOperation("createTable")>]
    member this.CreateTable(x, name) = Migration.create_table x name
    [<CustomOperation("addColumn", MaintainsVariableSpaceUsingBind = true)>]
    member this.AddColumn(x, name, tp) = Migration.add_column x name tp
    [<CustomOperation("primaryKey", MaintainsVariableSpaceUsingBind = true)>]
    member this.PrimaryKey(x, name, tp) = Migration.primaryKey x name tp
    [<CustomOperation("unique", MaintainsVariableSpaceUsingBind = true)>]
    member this.Unique(x, name, columns) = Migration.unique x name columns

let migration = new MigrationBuilder()

