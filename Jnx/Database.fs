module Jnx.Database

open Npgsql
open System.Configuration
open System.Data

type Country = {
    Id : int
    Code : string
    Name : string
    Genitive : string
    }

let toCountry (reader : NpgsqlDataReader) =
    { Id = unbox reader.["Id"]
      Code = unbox reader.["Code"]
      Name = unbox reader.["Name"]
      Genitive = unbox reader.["Genitive"] }

let connectionString = ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString

let QueryWithConnectionString (connectionString : string) (toType : NpgsqlDataReader -> 'T) (sql : string) =
    seq {
        use connection = new NpgsqlConnection(connectionString)
        use command = new NpgsqlCommand(sql, connection, CommandType = CommandType.Text)
        connection.Open()
        use reader = command.ExecuteReader()
        while reader.Read() do
            yield reader |> toType
    }

let Query toType = QueryWithConnectionString connectionString toType
