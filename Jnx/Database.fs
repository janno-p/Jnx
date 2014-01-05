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

let QueryWithConnectionString (connectionString : string) (toType : NpgsqlDataReader -> 'T) (sql : string) (args : (string * 'U) list) =
    seq {
        use connection = new NpgsqlConnection(connectionString)
        use command = new NpgsqlCommand(sql, connection, CommandType = CommandType.Text)
        args |> Seq.iter (fun (name, value) ->
            command.Parameters.Add(name, value) |> ignore
        )
        connection.Open()
        use reader = command.ExecuteReader()
        while reader.Read() do
            yield reader |> toType
    }

let Query toType = QueryWithConnectionString connectionString toType

let QueryNominalValues () =
    let queryString = @"select distinct nominal_value
                         from coins_coin
                         where nominal_value is not null
                         order by nominal_value desc"
    Query (fun reader -> reader?nominal_value) queryString List<string * obj>.Empty

let QueryCommemorativeYears () =
    let queryString = @"select distinct commemorative_year
                         from coins_coin
                         where commemorative_year is not null
                         order by commemorative_year desc"
    Query (fun reader -> reader?commemorative_year) queryString List<string * obj>.Empty

let QueryCountries () =
    let queryString = @"select c.id as Id,
                               c.code as Code,
                               c.name as Name,
                               c.genitive as Genitive
                         from coins_country c
                         order by c.name asc"
    Query toCountry queryString List<string * obj>.Empty

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
    Query toCountryStats queryString List<string * obj>.Empty

let QueryCountryByCode code =
    let queryString = @"select c.id as Id,
                               c.code as Code,
                               c.name as Name,
                               c.genitive as Genitive
                        from coins_country c
                        where c.code = :code"
    Query toCountry queryString [("code", code)] |> Seq.tryFind (fun _ -> true)

type Coin = { Id : int
              NominalValue : decimal
              Image : string
              CollectedAt : System.DateTime option
              CollectedBy : string option }

type CoinType =
    | CommonCoin of Coin * Country
    | CommemorativeCoin of Coin * Country * int

let toCommemorativeCoin (reader : NpgsqlDataReader) =
    printfn "%O" ((reader?CoinNominalValue).GetType())
    printfn "%O" ((reader?CoinCollectedAt).GetType())
    let coin = { Id = reader?CoinId
                 NominalValue = reader?CoinNominalValue
                 Image = reader?CoinImage
                 CollectedAt = match reader?CoinCollectedAt with
                               | :? System.DBNull -> None
                               | x -> Some x
                 CollectedBy = reader?CoinCollectedBy }
    let country = { Id = reader?CountryId
                    Code = reader?CountryCode
                    Name = reader?CountryName
                    Genitive = reader?CountryGenitive }
    printfn "%O" ((reader?CoinCommemorativeYear).GetType())
    CommemorativeCoin(coin, country, int reader?CoinCommemorativeYear)

let QueryCoinsByCommemorativeYear year =
    let qry = @"select  coin.id as CoinId,
                        coin.nominal_value as CoinNominalValue,
                        coin.image as CoinImage,
                        coin.commemorative_year as CoinCommemorativeYear,
                        coin.collected_at as CoinCollectedAt,
                        coin.collected_by as CoinCollectedBy,
                        country.id as CountryId,
                        country.code as CountryCode,
                        country.name as CountryName,
                        country.genitive as CountryGenitive
                  from  coins_coin as coin
            inner join  coins_country as country on country.id = coin.country_id
                 where  coin.commemorative_year = :year"
    match Query toCommemorativeCoin qry [("year", year)] |> Seq.toArray with
    | [||] -> None
    | x -> Some x
