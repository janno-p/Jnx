#I "../packages/FAKE.2.4.8.0/tools"
#I "../packages/Npgsql.2.0.14.3/lib/net40"

#r "FakeLib.dll"
#r "Npgsql.dll"
#r "System.Configuration.dll"

open Fake
open Npgsql

let OpenConnection () =
    let connectionStringName = "Jnx"
    let x = readConfig(__SOURCE_DIRECTORY__ @@ "../Jnx/ConnectionStrings.config")
    match x.SelectSingleNode(sprintf "//connectionStrings/add[@name='%s']/@connectionString" connectionStringName) with
    | null -> failwith (sprintf @"ConnectionString named ""%s"" was not found in configuration settings." connectionStringName)
    | node -> trace node.Value


Target "Test" (fun _ ->
    trace "Testing stuff ..."
    OpenConnection()
)

Target "Deploy" (fun _ ->
    trace "Heavy deploy action"
)

"Test" ==> "Deploy"

Run "Deploy"
