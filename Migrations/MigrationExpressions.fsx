open System
open System.Text

module Migration =
    type ColumnOptions = {
        Name : string
        SampleValue : obj
    }

    type ConstraintOptions = {
        Name : string
    }

    type DatabaseType<'T> =
        | Nullable of 'T
        | NotNullable of 'T

    type MigrationCommand = {
        CreateTable : bool
        TableName : string
        Columns : ColumnOptions list
        Constraints : ConstraintOptions list
    }

    let getTypeMapping (value : obj) =
        match value with
        | :? string -> "VARCHAR2"
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
        x()
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

let migration = new MigrationBuilder()

