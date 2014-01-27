module Jnx.Database

open FSharp.Data.Sql
open Npgsql
open System
open System.Configuration
open System.Data

type sql = SqlDataProvider<"Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx; Pooling=false;",
                           Common.DatabaseProviderTypes.POSTGRESQL,
                           @"/opt/monodevelop/lib/mono/4.5",
                           1000,
                           true>

let db = sql.GetDataContext()

let QueryNominalValues () =
    query {
        for coin in db.``[public].[coins_coin]`` do
        sortByDescending coin.nominal_value
        select coin.nominal_value
        distinct
    } |> Seq.toArray

let QueryCommemorativeYears () =
    query {
        for coin in db.``[public].[coins_coin]`` do
        where (coin.commemorative_year.IsSome)
        sortByDescending coin.commemorative_year
        select coin.commemorative_year.Value
        distinct
    } |> Seq.toArray

type Country =
        { Id : int
          Code : string
          Name : string
          Genitive : string }

let QueryCountries () =
    query {
        for country in db.``[public].[coins_country]`` do
        sortBy country.name
        select { Id = country.id
                 Code = country.code
                 Name = country.name
                 Genitive = country.genitive }
    } |> Seq.toArray

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

let GetDbValue value =
    let optionType = typedefof<option<_>>
    if value = null
        then null
    else
        let valueType = value.GetType()
        let valueProperty = valueType.GetProperty("Value")
        if valueType.IsGenericType && valueType.GetGenericTypeDefinition() = optionType then
            valueProperty.GetValue(value, [| |])
        else value

let ExecuteWithConnectionString (connectionString : string) (sql : string) (args : (string * obj) list) =
    use connection = new NpgsqlConnection(connectionString)
    use command = new NpgsqlCommand(sql, connection, CommandType = CommandType.Text)
    args |> Seq.iter (fun (name, value) ->
        command.Parameters.Add(name, (GetDbValue value)) |> ignore
    )
    connection.Open()
    command.ExecuteNonQuery()

let Query toType = QueryWithConnectionString connectionString toType
let Execute = ExecuteWithConnectionString connectionString

module Types =
    type CoinType =
        | CommonCoin of decimal
        | CommemorativeCoin of int * bool

    let addFileSuffix (fileName : string) suffix = fileName.Insert(fileName.Length - 4, suffix)

    type Coin =
        { Id : int
          Type : CoinType
          Country : Country
          Image : string
          ForTrade : int
          CollectedAt : DateTime option
          CollectedBy : string option }
        member this.ImageUri with get() = match this.CollectedAt with
                                          | Some _ -> addFileSuffix this.Image "_collected"
                                          | None -> this.ImageThumbUri
        member this.ImageThumbUri with get() = addFileSuffix this.Image "_thumb"
        member this.CollectedByValue with get() = match this.CollectedBy with
                                                  | Some str -> str
                                                  | _ -> ""
        member this.CollectedAtValue with get() = match this.CollectedAt with
                                                  | Some d -> d.ToString("dd.MM.yyyy HH:mm:ss")
                                                  | _ -> ""

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
        let coinType =
            match reader.Optional?CoinCommemorativeYear with
            | Some year -> CommemorativeCoin (year, false)
            | _ -> CommonCoin reader?CoinNominalValue
        { Id = reader?CoinId
          Type = coinType
          Image = reader?CoinImage
          ForTrade = 0
          CollectedAt = reader.Optional?CoinCollectedAt
          CollectedBy = reader.Optional?CoinCollectedBy
          Country = toCountry reader }

module Queries =
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
        Query Conversions.toCoin qry [("year", year)]

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
        Query Conversions.toCoin qry [("value", nominalValue)]

    let QueryCoinsOfCountry (country : Country) =
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
                     where  country.id = :country_id
                  order by  coin.commemorative_year desc,
                            coin.nominal_value desc"
        Query Conversions.toCoin qry [("country_id", country.Id)]
        |> Seq.toList
        |> List.partition (fun x -> match x.Type with | CommonCoin _ -> true | _ -> false)

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
        Query Conversions.toCoin qry [("id", id)] |> Seq.tryFind (fun _ -> true)

    let UpdateCoin (coin : Coin) =
        let qry = @"update  coins_coin
                       set  collected_by = :collected_by,
                            collected_at = :collected_at
                     where  id = :id"
        Execute qry [("collected_by", box coin.CollectedBy)
                     ("collected_at", box coin.CollectedAt)
                     ("id", box coin.Id)]
