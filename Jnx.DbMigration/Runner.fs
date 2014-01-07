module Jnx.DbMigration.Runner

open Npgsql
open System.Configuration

let connectionStringName = "Jnx"

let unless condition = not condition

let ConnectionStrings () =
    [ for x in ConfigurationManager.ConnectionStrings -> x ]

let OpenConnection () =
    match ConnectionStrings () |> Seq.tryFind (fun x -> x.Name = connectionStringName) with
    | Some connectionString ->
        let connection = new NpgsqlConnection(connectionString.ConnectionString)
        connection.Open()
        connection
    | _ -> failwith @"ConnectionString named ""Jnx"" was not found in configuration settings."

let IsInitialized (connection : NpgsqlConnection) =
    use command = connection.CreateCommand()
    command.CommandText <- @"select count(*) from information_schema.tables where table_catalog=:schemaname and table_name='migrations'"
    command.Parameters.AddWithValue("schemaname",  connection.Database) |> ignore
    unbox<int64> (command.ExecuteScalar()) > 0L

let public InitDatabase () =
    use connection = OpenConnection()
    if IsInitialized connection then
        failwith @"Schema is already initialized."
    use command = new NpgsqlCommand(@"create table migrations (migration varchar (1000) not null, constraint pk_migrations primary key (migration))", connection)
    connection.Open()
    command.ExecuteNonQuery() |> ignore
    printfn "Ready!"

let public RunMigrations () =
    use connection = OpenConnection()
    if not (IsInitialized connection) then
        failwith @"Schema is not properly initialized. Run initialization (/I) first."
    printfn "Yeppi!!"
