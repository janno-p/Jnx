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

let connectionString = ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString

let QueryWithConnectionString (connectionString : string) (toType : DynamicDataReader -> 'T) (sql : string) (args : (string * 'U) list) =
    seq {
        use connection = new NpgsqlConnection(connectionString)
        use command = new NpgsqlCommand(sql, connection, CommandType = CommandType.Text)
        args |> Seq.iter (fun (name, value) ->
            command.Parameters.Add(name, value) |> ignore
        )
        connection.Open()
        use reader = new DynamicDataReader(command.ExecuteReader())
        while reader.Read() do
            yield reader |> toType
    }

let Query toType = QueryWithConnectionString connectionString toType

module Types =
    [<AbstractClass>]
    type Coin () =
        member val Id = 0 with get, set
        member val NominalValue = 0M with get, set
        member val Image = "" with get, set
        member val CollectedAt = None : DateTime option with get, set
        member val CollectedBy = None : string option with get, set
        member x.ImageUri
            with get () =
                match x.CollectedAt with
                | Some _ -> x.Image.Insert(x.Image.Length - 4, "_collected")
                | None -> x.Image.Insert(x.Image.Length - 4, "_thumb")

    type CommonCoin () =
        inherit Coin ()

    type CommemorativeCoin () =
        inherit Coin ()
        member val Year = 0 with get, set
        member val IsCommon = false with get, set

    type Country = { Id : int
                     Code : string
                     Name : string
                     Genitive : string }

    type CoinOfCountry = {
        Coin : Coin
        Country : Country
    }

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

open Types

module Conversions =
    let toCountry (reader : DynamicDataReader) =
        { Id = reader?CountryId
          Code = reader?CountryCode
          Name = reader?CountryName
          Genitive = reader?CountryGenitive }

    let toCountryStats (reader : DynamicDataReader) =
        { Country = toCountry reader
          CollectedCommon = reader?CollectedCommon
          TotalCommon = reader?TotalCommon
          CollectedCommemorative = reader?CollectedCommemorative
          TotalCommemorative = reader?TotalCommemorative }

    let toCoin (reader : DynamicDataReader) =
        let coin =
            match reader.Optional?CoinCommemorativeYear with
            | Some year -> new CommemorativeCoin(Year = year) :> Coin
            | _ -> new CommonCoin() :> Coin
        coin.Id <- reader?CoinId
        coin.NominalValue <- reader?CoinNominalValue
        coin.Image <- reader?CoinImage
        coin.CollectedAt <- reader.Optional?CoinCollectedAt
        coin.CollectedBy <- reader.Optional?CoinCollectedBy
        coin

    let toCoinOfCountry (reader : DynamicDataReader) =
        { Coin = toCoin reader; Country = toCountry reader }

module Queries =
    let QueryCountries () =
        let qry = @"select  country.id as CountryId,
                            country.code as CountryCode,
                            country.name as CountryName,
                            country.genitive as CountryGenitive
                      from  coins_country country
                  order by  country.name asc"
        Query Conversions.toCountry qry List<string * obj>.Empty

    let QueryCountryByCode code =
        let qry = @"select  country.id as CountryId,
                            country.code as CountryCode,
                            country.name as CountryName,
                            country.genitive as CountryGenitive
                      from  coins_country country
                     where  country.code = :code"
        Query Conversions.toCountry qry [("code", code)] |> Seq.tryFind (fun _ -> true)

    let QueryCountryStats () =
        let qry = @"select  country.id as CountryId,
                            country.code as CountryCode,
                            country.name as CountryName,
                            country.genitive as CountryGenitive,
                            (select count(*) from coins_coin where country_id = country.id and commemorative_year is null) as TotalCommon,
                            (select count(*) from coins_coin where country_id = country.id and collected_at is not null and commemorative_year is null) as CollectedCommon,
                            (select count(*) from coins_coin where country_id = country.id and commemorative_year is not null) as TotalCommemorative,
                            (select count(*) from coins_coin where country_id = country.id and collected_at is not null and commemorative_year is not null) as CollectedCommemorative
                      from  coins_country country
                  order by  country.name asc"
        Query Conversions.toCountryStats qry List<string * obj>.Empty

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
        Query Conversions.toCoinOfCountry qry [("year", year)]

    let QueryCoinsByNominalValue nominalValue =
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
                     where  coin.nominal_value = :value and coin.commemorative_year is null
                  order by  country.name asc"
        Query Conversions.toCoinOfCountry qry [("value", nominalValue)]

    let QueryCoinsOfCountry (country : Country) =
        let qry = @"select  coin.id as CoinId,
                            coin.nominal_value as CoinNominalValue,
                            coin.image as CoinImage,
                            coin.collected_at as CoinCollectedAt,
                            coin.collected_by as CoinCollectedBy,
                            coin.commemorative_year as CoinCommemorativeYear
                      from  coins_coin as coin
                     where  coin.country_id = :country_id
                  order by  coin.commemorative_year desc,
                            coin.nominal_value desc"
        let coins = Query Conversions.toCoin qry [("country_id", country.Id)] |> Seq.toList
        (coins |> List.filter (fun x -> x :? CommonCoin), coins |> List.filter (fun x -> x :? CommemorativeCoin))

    let QueryCoinById id =
        let qry = @"select  coin.id as CoinId,
                            coin.nominal_value as CoinNominalValue,
                            coin.image as CoinImage,
                            coin.collected_at as CoinCollectedAt,
                            coin.collected_by as CoinCollectedBy,
                            coin.commemorative_year as CoinCommemorativeYear,
                            country.id as CountryId,
                            country.code as CountryCode,
                            country.name as CountryName,
                            country.genitive as CountryGenitive
                      from  coins_coin as coin
                            inner join  coins_country as country on country.id = coin.country_id
                     where  coin.id = :id"
        Query Conversions.toCoinOfCountry qry [("id", id)] |> Seq.tryFind (fun _ -> true)

    let QueryNominalValues () =
        let qry = @"select distinct  nominal_value
                               from  coins_coin
                              where  nominal_value is not null
                           order by  nominal_value desc"
        Query (fun reader -> unbox<decimal> reader?nominal_value) qry List<string * obj>.Empty

    let QueryCommemorativeYears () =
        let qry = @"select distinct  commemorative_year
                               from  coins_coin
                              where  commemorative_year is not null
                           order by  commemorative_year desc"
        Query (fun reader -> reader?commemorative_year) qry List<string * obj>.Empty
