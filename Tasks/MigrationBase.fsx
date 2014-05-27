#I "../Jnx.Tasks/bin/Debug"
#I "../packages/FAKE.2.4.8.0/tools"
#I "../packages/Npgsql.2.1.3/lib/net45"

#r "FakeLib"
#r "Jnx.Tasks"
#r "Npgsql"
#r "System.Data"

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
