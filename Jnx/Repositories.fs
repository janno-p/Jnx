module Jnx.Repositories

open FSharp.Data.Sql
open FSharp.Data.Sql.Common
open System
open System.Configuration

type Sql = SqlDataProvider<DatabaseProviderTypes.POSTGRESQL,
                           "Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx;",
                           ResolutionPath = "/home/janno/Work/Jnx/packages/Npgsql.2.2.1/lib/net45",
                           IndividualsAmount = 100,
                           UseOptionTypes = true>

let db = Sql.GetDataContext(ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString)

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

type User =
    { Identifier : Guid
      Name : string option
      Email : string
      IsApproved : bool
      ProviderName : string
      ProviderIdentity : string
      Picture : string
      Roles : int }
    static member NewUser with get() = { Identifier = Guid.NewGuid()
                                         Name = None
                                         Email = ""
                                         IsApproved = false
                                         ProviderName = ""
                                         ProviderIdentity = ""
                                         Picture = ""
                                         Roles = 0 }

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
    let ToModel (country : Sql.dataContext.``[public].[coins_country]Entity``) =
        { Code = country.CODE; Name = country.NAME; Genitive = country.GENITIVE }

    let GetAll paging =
        let baseQuery = pquery { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do
                                 sortBy country.NAME }
        let pagingQuery = match paging with
                          | All -> baseQuery
                          | Range (f, t) -> pquery { for country in %baseQuery do
                                                     skip f
                                                     take (t - f) }
        query { for country in %pagingQuery do
                select { Code = country.CODE
                         Name = country.NAME
                         Genitive = country.GENITIVE } } |> Seq.toArray

    let Save (country : Country) =
        db.ClearUpdates() |> ignore
        let dbCountry = db.``[PUBLIC].[COINS_COUNTRY]``.Create(country.Genitive, country.Name)
        dbCountry.CODE <- country.Code
        db.SubmitUpdates()
        country

    let Update code columns =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do where (country.CODE = code) } |> Seq.head
        columns |> Seq.iter (fun (k, v) -> dbCountry.SetColumn(k, v))
        db.SubmitUpdates()
        dbCountry |> ToModel

    let GetByCode code =
        query { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do where (country.CODE = code) }
        |> Seq.map ToModel
        |> Seq.tryFind (fun _ -> true)

    let Delete code =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do where (country.CODE = code) } |> Seq.head
        dbCountry.Delete()
        db.SubmitUpdates()

module Coins =
    let GetCommemorativeYears () =
        query { for coin in db.``[PUBLIC].[COINS_COMMEMORATIVE_COIN]`` do
                sortByDescending coin.YEAR
                select coin.YEAR
                distinct } |> Seq.toArray

    let GetNominalValues () =
        query { for coin in db.``[PUBLIC].[COINS_COMMON_COIN]`` do
                sortByDescending coin.NOMINAL_VALUE
                select coin.NOMINAL_VALUE
                distinct } |> Seq.toArray

    let GetCountryStatistics () =
        query { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do
                sortBy country.NAME
                select { Country = { Code = country.CODE; Name = country.NAME; Genitive = country.GENITIVE }
                         TotalCommon = query { for coinage in db.``[PUBLIC].[COINS_COINAGE]`` do
                                               for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                               where (coinage.COUNTRY_CODE = country.CODE)
                                               count }
                         CollectedCommon = query { for coinage in db.``[PUBLIC].[COINS_COINAGE]`` do
                                                   for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                                   join coin in db.``[PUBLIC].[COINS_COIN]`` on (common_coin.COIN_ID = coin.ID)
                                                   where ((coinage.COUNTRY_CODE = country.CODE) && (coin.COLLECTED_AT.IsSome))
                                                   count }
                         TotalCommemorative = query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                                      count }
                         CollectedCommemorative = query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                                          join coin in db.``[PUBLIC].[COINS_COIN]`` on (commemorative_coin.COIN_ID = coin.ID)
                                                          where (coin.COLLECTED_AT.IsSome)
                                                          count } } }
        |> Seq.toArray

    let OfCountry country =
        let commonCoins = query { for common_coin in db.``[PUBLIC].[COINS_COMMON_COIN]`` do
                                  join coin in db.``[PUBLIC].[COINS_COIN]`` on (common_coin.COIN_ID = coin.ID)
                                  for coinage in common_coin.fk_coins_common_coin_coins_coinage do
                                  where (coinage.COUNTRY_CODE = country.Code)
                                  sortByDescending common_coin.NOMINAL_VALUE
                                  select (coin, common_coin, coinage) }
                          |> Seq.map (fun (coin, common_coin, coinage) -> 
                                { Id = coin.ID
                                  Type = CommonCoin({ Id = coinage.ID
                                                      Year = coinage.YEAR
                                                      Name = coinage.NAME
                                                      Country = country }, common_coin.NOMINAL_VALUE)
                                  ImageUri = coin.IMAGE_URI
                                  NumExtra = coin.NUM_EXTRA
                                  CollectedAt = coin.COLLECTED_AT
                                  CollectedBy = coin.COLLECTED_BY })
                          |> Seq.toArray
        let commemorativeCoins = query { for commemorative_coin in db.``[PUBLIC].[COINS_COMMEMORATIVE_COIN]`` do
                                         join coin in db.``[PUBLIC].[COINS_COIN]`` on (commemorative_coin.COIN_ID = coin.ID)
                                         where (commemorative_coin.COUNTRY_CODE = country.Code)
                                         sortByDescending commemorative_coin.YEAR
                                         select (coin, commemorative_coin) }
                                 |> Seq.map (fun (coin, commemorativeCoin) ->
                                        { Id = coin.ID
                                          Type = CommemorativeCoin(country, commemorativeCoin.YEAR, commemorativeCoin.COMMON_ISSUE)
                                          ImageUri = coin.IMAGE_URI
                                          NumExtra = coin.NUM_EXTRA
                                          CollectedAt = coin.COLLECTED_AT
                                          CollectedBy = coin.COLLECTED_BY })
                                 |> Seq.toArray
        (commonCoins, commemorativeCoins)

    let OfCommemorativeYear year =
        query { for commemorative_coin in db.``[PUBLIC].[COINS_COMMEMORATIVE_COIN]`` do
                where (commemorative_coin.YEAR = year)
                join coin in db.``[PUBLIC].[COINS_COIN]`` on (commemorative_coin.COIN_ID = coin.ID)
                join country in db.``[PUBLIC].[COINS_COUNTRY]`` on (commemorative_coin.COUNTRY_CODE = country.CODE)
                sortBy country.NAME
                sortBy coin.ID
                select { Id = coin.ID
                         Type = CommemorativeCoin({ Code = country.CODE
                                                    Name = country.NAME
                                                    Genitive = country.GENITIVE },
                                                  commemorative_coin.YEAR,
                                                  commemorative_coin.COMMON_ISSUE)
                         ImageUri = coin.IMAGE_URI
                         NumExtra = coin.NUM_EXTRA
                         CollectedAt = coin.COLLECTED_AT
                         CollectedBy = coin.COLLECTED_BY } } |> Seq.toArray

    let OfNominalValue nominalValue =
        query { for common_coin in db.``[PUBLIC].[COINS_COMMON_COIN]`` do
                where (common_coin.NOMINAL_VALUE = nominalValue)
                join coin in db.``[PUBLIC].[COINS_COIN]`` on (common_coin.COIN_ID = coin.ID)
                join coinage in db.``[PUBLIC].[COINS_COINAGE]`` on (common_coin.COINAGE_ID = coinage.ID)
                join country in db.``[PUBLIC].[COINS_COUNTRY]`` on (coinage.COUNTRY_CODE = country.CODE)
                sortBy country.NAME
                select { Id = coin.ID
                         Type = CommonCoin({ Id = coinage.ID
                                             Year = coinage.YEAR
                                             Name = coinage.NAME
                                             Country = { Code = country.CODE; Name = country.NAME; Genitive = country.GENITIVE } },
                                           common_coin.NOMINAL_VALUE)
                         ImageUri = coin.IMAGE_URI
                         NumExtra = coin.NUM_EXTRA
                         CollectedAt = coin.COLLECTED_AT
                         CollectedBy = coin.COLLECTED_BY } } |> Seq.toArray

    let GetById id =
        query { for coin in db.``[PUBLIC].[COINS_COIN]`` do
                where (coin.ID = id)
                for common_coin in (!!) coin.fk_coins_common_coin_coins_coin do
                for commemorative_coin in (!!) coin.fk_coins_commemorative_coin_coins_coin do
                select (coin, common_coin, commemorative_coin) }
        |> Seq.map (fun (coin, common_coin, commemorative_coin) ->
            match common_coin, commemorative_coin with
            | common_coin, null -> { Id = coin.ID
                                     Type = CommonCoin(unbox null, common_coin.NOMINAL_VALUE)
                                     ImageUri = coin.IMAGE_URI
                                     NumExtra = coin.NUM_EXTRA
                                     CollectedAt = coin.COLLECTED_AT
                                     CollectedBy = coin.COLLECTED_BY }
            | null, commemorative_coin -> { Id = coin.ID
                                            Type = CommemorativeCoin(unbox null, commemorative_coin.YEAR, commemorative_coin.COMMON_ISSUE)
                                            ImageUri = coin.IMAGE_URI
                                            NumExtra = coin.NUM_EXTRA
                                            CollectedAt = coin.COLLECTED_AT
                                            CollectedBy = coin.COLLECTED_BY }
            | _, _ -> failwith "Invalid coin entity found")
        |> Seq.tryFind (fun _ -> true)

module Users =
    let toModel (x : Sql.dataContext.``[public].[user]Entity``) =
        { Identifier = x.IDENTIFIER
          Name = x.NAME
          Email = x.EMAIL
          IsApproved = x.APPROVED
          ProviderName = x.PROVIDER_NAME
          ProviderIdentity = x.PROVIDER_IDENTITY
          Picture = x.PROVIDER_PICTURE
          Roles = x.ROLES }

    let Create (user : User) =
        db.ClearUpdates() |> ignore
        let dbUser = db.``[PUBLIC].[USER]``.Create(user.IsApproved, user.Email, user.Identifier, user.Picture, user.Roles)
        dbUser.PROVIDER_NAME <- user.ProviderName
        dbUser.PROVIDER_IDENTITY <- user.ProviderIdentity
        db.SubmitUpdates()
        user

    let GetByIdentity providerName identity =
        query { for user in db.``[PUBLIC].[USER]`` do
                where (user.PROVIDER_NAME = providerName && user.PROVIDER_IDENTITY = identity) }
        |> Seq.map toModel
        |> Seq.tryFind (fun _ -> true)

    let GetByIdentifier identifier =
        query { for user in db.``[PUBLIC].[USER]`` do
                where (user.IDENTIFIER = identifier) }
        |> Seq.map toModel
        |> Seq.tryFind (fun _ -> true)

    let Update user =
        let dbUser = query { for u in db.``[PUBLIC].[USER]`` do
                             where (u.PROVIDER_NAME = user.ProviderName && u.PROVIDER_IDENTITY = user.ProviderIdentity) }
                     |> Seq.tryFind (fun _ -> true)
        match dbUser with
        | Some dbUser ->
            db.ClearUpdates() |> ignore
            dbUser.APPROVED <- user.IsApproved
            dbUser.EMAIL <- user.Email
            dbUser.NAME <- user.Name
            dbUser.PROVIDER_PICTURE <- user.Picture
            dbUser.ROLES <- user.Roles
            db.SubmitUpdates()
        | _ -> ()
        user
