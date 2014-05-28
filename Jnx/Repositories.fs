module Jnx.Repositories

open FSharp.Data.Sql
open System
open System.Configuration

type sql = SqlDataProvider<"Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx;",
                           Common.DatabaseProviderTypes.POSTGRESQL,
                           @"/opt/mono-3.4.0/lib/mono/4.5",
                           100,
                           true>

let db = sql.GetDataContext(ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString)

type Country =
    { Code : string
      Name : string
      Genitive : string }

type Coinage =
    { Id : int
      Year : int16
      Name : string
      Country : Country }

type CoinType =
    | CommonCoin of Coinage * decimal
    | CommemorativeCoin of Country * int16 * bool

type Coin =
    { Id : int
      Type : CoinType
      ImageUri : string
      NumExtra : int16
      CollectedAt : DateTime option
      CollectedBy : string option }

type CountryCoinStatistics =
    { Country : Country
      CollectedCommon : int
      CollectedCommemorative : int
      TotalCommon : int
      TotalCommemorative : int }

// Partial query builder extensions from
// http://fpish.net/blog/loic.denuziere/id/3508/2013924-f-query-expressions-and-composability

type Linq.QueryBuilder with
    [<ReflectedDefinition>]
    member this.Source (queries : Linq.QuerySource<'T, _>) = queries

type PartialQueryBuilder () =
    inherit Linq.QueryBuilder()

    member this.Run (e : Quotations.Expr<Linq.QuerySource<'T, System.Linq.IQueryable>>) = e

let pquery = PartialQueryBuilder()

type Paging =
    | All
    | Range of int * int

module Countries =
    let ToModel (country : sql.dataContext.``[public].[coins_country]Entity``) =
        { Code = country.code; Name = country.name; Genitive = country.genitive }

    let GetAll paging =
        let baseQuery = pquery { for country in db.``[public].[coins_country]`` do
                                 sortBy country.name }
        let pagingQuery = match paging with
                          | All -> baseQuery
                          | Range (f, t) -> pquery { for country in %baseQuery do
                                                     skip f
                                                     take (t - f) }
        query { for country in %pagingQuery do
                select { Code = country.code
                         Name = country.name
                         Genitive = country.genitive } } |> Seq.toArray

    let Save (country : Country) =
        db.ClearUpdates() |> ignore
        let dbCountry = db.``[public].[coins_country]``.Create(country.Genitive, country.Name)
        dbCountry.code <- country.Code
        db.SubmitUpdates()
        country

    let Update code columns =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[public].[coins_country]`` do where (country.code = code) } |> Seq.head
        columns |> Seq.iter (fun (k, v) -> dbCountry.SetColumn(k, v))
        db.SubmitUpdates()
        dbCountry |> ToModel

    let GetByCode code =
        query { for country in db.``[public].[coins_country]`` do where (country.code = code) }
        |> Seq.map ToModel
        |> Seq.tryFind (fun _ -> true)

    let Delete code =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[public].[coins_country]`` do where (country.code = code) } |> Seq.head
        dbCountry.Delete()
        db.SubmitUpdates()

module Coins =
    let GetCommemorativeYears () =
        query { for coin in db.``[public].[coins_commemorative_coin]`` do
                sortByDescending coin.year
                select coin.year
                distinct } |> Seq.toArray

    let GetNominalValues () =
        query { for coin in db.``[public].[coins_common_coin]`` do
                sortByDescending coin.nominal_value
                select coin.nominal_value
                distinct } |> Seq.toArray

    let GetCountryStatistics () =
        query { for country in db.``[public].[coins_country]`` do
                sortBy country.name
                select (country,
                        query { for coinage in country.fk_coins_coinage_coins_country do
                                for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                count },
                        query { for coinage in country.fk_coins_coinage_coins_country do
                                for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                join coin in db.``[public].[coins_coin]`` on (common_coin.coin_id = coin.id)
                                where coin.collected_at.IsSome
                                count },
                        query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                count },
                        query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                join coin in db.``[public].[coins_coin]`` on (commemorative_coin.coin_id = coin.id)
                                where coin.collected_at.IsSome
                                count }) }
        |> Seq.map (fun (country, totalCommon, collectedCommon, totalCommemorative, collectedCommemorative) ->
            { Country = { Code = country.code; Name = country.name; Genitive = country.genitive }
              TotalCommon = totalCommon
              CollectedCommon = collectedCommon
              TotalCommemorative = totalCommemorative
              CollectedCommemorative = collectedCommemorative } )
        |> Seq.toArray

    let OfCountry country =
        let commonCoins = query { for common_coin in db.``[public].[coins_common_coin]`` do
                                  join coin in db.``[public].[coins_coin]`` on (common_coin.coin_id = coin.id)
                                  for coinage in db.``[public].[coins_coinage]`` do
                                  where (coinage.country_code = country.Code)
                                  sortByDescending common_coin.nominal_value
                                  select { Id = coin.id
                                           Type = CommonCoin({ Id = coinage.id
                                                               Year = coinage.year
                                                               Name = coinage.name
                                                               Country = country },
                                                             common_coin.nominal_value)
                                           ImageUri = coin.image_uri
                                           NumExtra = coin.num_extra
                                           CollectedAt = coin.collected_at
                                           CollectedBy = coin.collected_by } } |> Seq.toArray
        let commemorativeCoins = query { for commemorative_coin in db.``[public].[coins_commemorative_coin]`` do
                                         join coin in db.``[public].[coins_coin]`` on (commemorative_coin.coin_id = coin.id)
                                         where (commemorative_coin.country_code = country.Code)
                                         sortByDescending commemorative_coin.year
                                         select { Id = coin.id
                                                  Type = CommemorativeCoin(country, commemorative_coin.year, commemorative_coin.common_issue)
                                                  ImageUri = coin.image_uri
                                                  NumExtra = coin.num_extra
                                                  CollectedAt = coin.collected_at
                                                  CollectedBy = coin.collected_by } } |> Seq.toArray
        (commonCoins, commemorativeCoins)

    let OfCommemorativeYear year =
        query { for commemorative_coin in db.``[public].[coins_commemorative_coin]`` do
                where (commemorative_coin.year = year)
                join coin in db.``[public].[coins_coin]`` on (commemorative_coin.coin_id = coin.id)
                join country in db.``[public].[coins_country]`` on (commemorative_coin.country_code = country.code)
                sortBy country.name
                sortBy coin.id
                select { Id = coin.id
                         Type = CommemorativeCoin({ Code = country.code
                                                    Name = country.name
                                                    Genitive = country.genitive },
                                                  commemorative_coin.year,
                                                  commemorative_coin.common_issue)
                         ImageUri = coin.image_uri
                         NumExtra = coin.num_extra
                         CollectedAt = coin.collected_at
                         CollectedBy = coin.collected_by } } |> Seq.toArray

    let OfNominalValue nominalValue =
        query { for common_coin in db.``[public].[coins_common_coin]`` do
                where (common_coin.nominal_value = nominalValue)
                join coin in db.``[public].[coins_coin]`` on (common_coin.coin_id = coin.id)
                join coinage in db.``[public].[coins_coinage]`` on (common_coin.coinage_id = coinage.id)
                join country in db.``[public].[coins_country]`` on (coinage.country_code = country.code)
                sortBy country.name
                select { Id = coin.id
                         Type = CommonCoin({ Id = coinage.id
                                             Year = coinage.year
                                             Name = coinage.name
                                             Country = { Code = country.code; Name = country.name; Genitive = country.genitive } },
                                           common_coin.nominal_value)
                         ImageUri = coin.image_uri
                         NumExtra = coin.num_extra
                         CollectedAt = coin.collected_at
                         CollectedBy = coin.collected_by } } |> Seq.toArray

    let GetById id =
        query { for coin in db.``[public].[coins_coin]`` do
                where (coin.id = id)
                for common_coin in (!!) coin.fk_coins_common_coin_coins_coin do
                for commemorative_coin in (!!) coin.fk_coins_commemorative_coin_coins_coin do
                select (coin, common_coin, commemorative_coin) }
        |> Seq.map (fun (coin, common_coin, commemorative_coin) ->
            match common_coin, commemorative_coin with
            | common_coin, null -> { Id = coin.id
                                     Type = CommonCoin(unbox null, common_coin.nominal_value)
                                     ImageUri = coin.image_uri
                                     NumExtra = coin.num_extra
                                     CollectedAt = coin.collected_at
                                     CollectedBy = coin.collected_by }
            | null, commemorative_coin -> { Id = coin.id
                                            Type = CommemorativeCoin(unbox null, commemorative_coin.year, commemorative_coin.common_issue)
                                            ImageUri = coin.image_uri
                                            NumExtra = coin.num_extra
                                            CollectedAt = coin.collected_at
                                            CollectedBy = coin.collected_by }
            | _, _ -> failwith "Invalid coin entity found")
        |> Seq.tryFind (fun _ -> true)
