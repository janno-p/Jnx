#I "../packages/FAKE.2.4.8.0/tools"
#I "../packages/Npgsql.2.0.14.3/lib/net40"

#r "FakeLib.dll"
#r "Npgsql.dll"
#r "System.Data.dll"

open Fake
open Npgsql

let OpenConnection () =
    let connectionStringName = "Jnx"
    let x = readConfig(__SOURCE_DIRECTORY__ @@ "../Jnx/ConnectionStrings.config")
    match x.SelectSingleNode(sprintf "//connectionStrings/add[@name='%s']/@connectionString" connectionStringName) with
    | null -> failwith (sprintf @"ConnectionString named ""%s"" was not found in configuration settings." connectionStringName)
    | node ->
        let connection = new NpgsqlConnection(node.Value)
        connection.Open()
        connection

let IsInitialized (connection : NpgsqlConnection) =
    use command = connection.CreateCommand()
    command.CommandText <- @"SELECT COUNT(*) FROM information_schema.tables WHERE table_catalog=:schemaname AND table_name='migrations'"
    command.Parameters.Add("schemaname", connection.Database) |> ignore
    unbox<int64> (command.ExecuteScalar()) > 0L

Target "Init" (fun _ ->
    use connection = OpenConnection()
    if IsInitialized connection then
        failwith "Schema is already initialized."
    trace "Yeppi!!"
)

Target "Migrate" (fun _ ->
    use connection = OpenConnection()
    if not (IsInitialized connection) then
        run "Init"
    trace "Yeppi!!"
)

RunTargetOrDefault "Migrate"
