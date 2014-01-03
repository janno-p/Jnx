module Jnx.Database

open Npgsql
open System.Configuration
open System.Data

let (?) (reader : NpgsqlDataReader) (name : string) =
    unbox reader.[name]

type Country = {
    Id : int
    Code : string
    Name : string
    Genitive : string
    }

let toCountry (reader : NpgsqlDataReader) =
    { Id = reader?Id
      Code = reader?Code
      Name = reader?Name
      Genitive = reader?Genitive }

type CountryStats =
    { Country : Country
      CollectedCommon : int64
      CollectedCommemorative : int64
      TotalCommon : int64
      TotalCommemorative : int64 }
    member x.CommonPercent with get () = match x.TotalCommon with | 0L -> 100 | _ -> int (x.CollectedCommon * 100L / x.TotalCommon)
    member x.CommemorativePercent with get () = match x.TotalCommemorative with | 0L -> 100 | _ -> int (x.CollectedCommemorative * 100L / x.TotalCommemorative)

let toCountryStats (reader : NpgsqlDataReader) =
    { Country = toCountry reader
      CollectedCommon = reader?CollectedCommon
      TotalCommon = reader?TotalCommon
      CollectedCommemorative = reader?CollectedCommemorative
      TotalCommemorative = reader?TotalCommemorative }

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

let QueryCountryStats () =
    let queryString = @"select c.id as Id,
                                c.code as Code,
                                c.name as Name,
                                c.genitive as Genitive,
                                (select count(*) from coins_coin where country_id = c.id and commemorative_year is null) as TotalCommon,
                                (select count(*) from coins_coin where country_id = c.id and collected_at is not null and commemorative_year is null) as CollectedCommon,
                                (select count(*) from coins_coin where country_id = c.id and commemorative_year is not null) as TotalCommemorative,
                                (select count(*) from coins_coin where country_id = c.id and collected_at is not null and commemorative_year is not null) as CollectedCommemorative
                         from coins_country c
                         order by c.name asc"
    Query toCountryStats queryString
