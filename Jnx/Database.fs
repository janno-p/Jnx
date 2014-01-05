module Jnx.Database

open Npgsql
open System
open System.Configuration
open System.Data

type DynamicOptionalDataReader(reader : IDataReader) =
    member private x.Reader = reader
    static member (?) (r : DynamicOptionalDataReader, name : string) : 'T option =
        match r.Reader.[name] with
        | :? System.DBNull -> None
        | value -> Some (unbox value)

type DynamicDataReader(reader : IDataReader) =
    member private x.Reader = reader
    member x.Read() = reader.Read()
    member x.Optional = new DynamicOptionalDataReader(reader)
    static member (?) (r : DynamicDataReader, name : string) : 'T =
        unbox r.Reader.[name]
    interface IDisposable with
        member x.Dispose() = reader.Dispose()

type Country = { Id : int
                 Code : string
                 Name : string
                 Genitive : string }

let toCountry (reader : DynamicDataReader) =
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
    member x.CommonPercent with get () =
                            match x.TotalCommon with
                            | 0L -> 100
                            | _ -> int (x.CollectedCommon * 100L / x.TotalCommon)
    member x.CommemorativePercent with get () =
                                    match x.TotalCommemorative with
                                    | 0L -> 100
                                    | _ -> int (x.CollectedCommemorative * 100L / x.TotalCommemorative)

let toCountryStats (reader : DynamicDataReader) =
    { Country = toCountry reader
      CollectedCommon = reader?CollectedCommon
      TotalCommon = reader?TotalCommon
      CollectedCommemorative = reader?CollectedCommemorative
      TotalCommemorative = reader?TotalCommemorative }

let connectionString = ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString

let QueryWithConnectionString (connectionString : string) (toType : DynamicDataReader -> 'T) (sql : string) (args : (string * 'U) list) =
    seq {
        use connection = new NpgsqlConnection(connectionString)
        use command = new NpgsqlCommand(sql, connection, CommandType = CommandType.Text)
        args |> Seq.iter (fun (name, value) ->
            command.Parameters.AddWithValue(name, value) |> ignore
        )
        connection.Open()
        use reader = new DynamicDataReader(command.ExecuteReader())
        while reader.Read() do
            yield reader |> toType
    }

let Query toType = QueryWithConnectionString connectionString toType

let QueryNominalValues () =
    let queryString = @"select distinct nominal_value
                         from coins_coin
                         where nominal_value is not null
                         order by nominal_value desc"
    Query (fun reader -> unbox<decimal> reader?nominal_value) queryString List<string * obj>.Empty

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

type CommonCoin = { Coin : Coin
                    Country : Country }

type CommemorativeCoin = { Coin : Coin
                           Country : Country
                           Year : int }

let toCommemorativeCoin (reader : DynamicDataReader) =
    let coin = { Id = reader?CoinId
                 NominalValue = reader?CoinNominalValue
                 Image = reader?CoinImage
                 CollectedAt = reader.Optional?CoinCollectedAt
                 CollectedBy = reader.Optional?CoinCollectedBy }
    let country = { Id = reader?CountryId
                    Code = reader?CountryCode
                    Name = reader?CountryName
                    Genitive = reader?CountryGenitive }
    { Coin = coin; Country = country; Year = reader?CoinCommemorativeYear }

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
                 where  coin.commemorative_year = :year
              order by  country.name asc,
                        coin.id asc"
    Query toCommemorativeCoin qry [("year", year)]

let toCommonCoin (reader : DynamicDataReader) : CommonCoin =
    let coin = { Id = reader?CoinId
                 NominalValue = reader?CoinNominalValue
                 Image = reader?CoinImage
                 CollectedAt = reader.Optional?CoinCollectedAt
                 CollectedBy = reader.Optional?CoinCollectedBy }
    let country = { Id = reader?CountryId
                    Code = reader?CountryCode
                    Name = reader?CountryName
                    Genitive = reader?CountryGenitive }
    { Coin = coin; Country = country }

let QueryCoinsByNominalValue nominalValue =
    let qry = @"select  coin.id as CoinId,
                        coin.nominal_value as CoinNominalValue,
                        coin.image as CoinImage,
                        coin.collected_at as CoinCollectedAt,
                        coin.collected_by as CoinCollectedBy,
                        country.id as CountryId,
                        country.code as CountryCode,
                        country.name as CountryName,
                        country.genitive as CountryGenitive
                  from  coins_coin as coin
            inner join  coins_country as country on country.id = coin.country_id
                 where  coin.nominal_value = :value and coin.commemorative_year is null
              order by  country.name asc"
    Query toCommonCoin qry [("value", nominalValue)]
