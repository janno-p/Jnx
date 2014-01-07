module Jnx.DbMigration.Runner

open Npgsql
open System.Configuration

let private OpenConnection () =
    let connectionStringName = "Jnx"
    let connectionStrings = [ for x in ConfigurationManager.ConnectionStrings -> x ]
    match connectionStrings |> Seq.tryFind (fun x -> x.Name = connectionStringName) with
    | Some connectionString ->
        let connection = new NpgsqlConnection(connectionString.ConnectionString)
        connection.Open()
        connection
    | _ -> failwith (sprintf @"ConnectionString named ""%s"" was not found in configuration settings." connectionStringName)

let private IsInitialized (connection : NpgsqlConnection) =
    use command = connection.CreateCommand()
    command.CommandText <- @"select count(*) from information_schema.tables where table_catalog=:schemaname and table_name='migrations'"
    command.Parameters.AddWithValue("schemaname",  connection.Database) |> ignore
    unbox<int64> (command.ExecuteScalar()) > 0L

let InitDatabase () =
    use connection = OpenConnection()
    if IsInitialized connection then
        failwith @"Schema is already initialized."
    use command = new NpgsqlCommand(@"create table migrations (migration varchar (1000) not null, constraint pk_migrations primary key (migration))", connection)
    connection.Open()
    command.ExecuteNonQuery() |> ignore
    printfn "Ready!"

let RunMigrations () =
    use connection = OpenConnection()
    if not (IsInitialized connection) then
        failwith @"Schema is not properly initialized. Run initialization (/I) first."
    printfn "Yeppi!!"
