#I "../packages/FAKE.2.4.8.0/tools"
#I "../packages/Npgsql.2.1.3/lib/net45"
#I "../packages/FsSql"

#r "FakeLib"
#r "FsSql"
#r "Npgsql"
#r "System.Data"

open Fake
open Npgsql
open System.Data

let connectionString =
    let connectionStringName = "Jnx"
    let config = readConfig(__SOURCE_DIRECTORY__ @@ "../Jnx/ConnectionStrings.config")
    match config.SelectSingleNode(sprintf "//connectionStrings/add[@name='%s']/@connectionString" connectionStringName) with
    | null -> failwith (sprintf @"ConnectionString named ""%s"" was not found in configuration settings." connectionStringName)
    | node -> node.Value

let openConnection () =
    let connection = new NpgsqlConnection(connectionString)
    connection.Open()
    connection :> IDbConnection

let connectionManager = Sql.withNewConnection(openConnection)
let sql = SqlWrapper(connectionManager)
let exec sqlCommand = sql.ExecNonQuery sqlCommand [] |> ignore
let param = Sql.Parameter.make
