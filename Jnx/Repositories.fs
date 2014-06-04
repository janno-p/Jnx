module Jnx.Repositories

open FSharp.Data.Sql
open System
open System.Configuration

type sql = SqlDataProvider<"Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx;",
                           Common.DatabaseProviderTypes.POSTGRESQL,
                           @"/home/janno/Work/Jnx/packages/Npgsql.2.1.3/lib/net45",
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
                select country }
        |> Seq.map (fun country ->
            { Country = { Code = country.code; Name = country.name; Genitive = country.genitive }
              TotalCommon = query { for coinage in db.``[public].[coins_coinage]`` do
                                    for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                    where (coinage.country_code = country.code)
                                    count }
              CollectedCommon = query { for coinage in db.``[public].[coins_coinage]`` do
                                        for common_coin in coinage.fk_coins_common_coin_coins_coinage do
                                        join coin in db.``[public].[coins_coin]`` on (common_coin.coin_id = coin.id)
                                        where ((coinage.country_code = country.code) && (coin.collected_at.IsSome))
                                        count }
              TotalCommemorative = query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                           count }
              CollectedCommemorative = query { for commemorative_coin in country.fk_coins_commemorative_coin_coins_country do
                                               join coin in db.``[public].[coins_coin]`` on (commemorative_coin.coin_id = coin.id)
                                               where (coin.collected_at.IsSome)
                                               count } } )
        |> Seq.toArray

    let OfCountry country =
        let commonCoins = query { for common_coin in db.``[public].[coins_common_coin]`` do
                                  join coin in db.``[public].[coins_coin]`` on (common_coin.coin_id = coin.id)
                                  for coinage in common_coin.fk_coins_common_coin_coins_coinage do
                                  where (coinage.country_code = country.Code)
                                  sortByDescending common_coin.nominal_value
                                  select (coin, common_coin, coinage) }
                          |> Seq.map (fun (coin, common_coin, coinage) -> 
                                { Id = coin.id
                                  Type = CommonCoin({ Id = coinage.id
                                                      Year = coinage.year
                                                      Name = coinage.name
                                                      Country = country }, common_coin.nominal_value)
                                  ImageUri = coin.image_uri
                                  NumExtra = coin.num_extra
                                  CollectedAt = coin.collected_at
                                  CollectedBy = coin.collected_by })
                          |> Seq.toArray
        let commemorativeCoins = query { for commemorative_coin in db.``[public].[coins_commemorative_coin]`` do
                                         join coin in db.``[public].[coins_coin]`` on (commemorative_coin.coin_id = coin.id)
                                         where (commemorative_coin.country_code = country.Code)
                                         sortByDescending commemorative_coin.year
                                         select (coin, commemorative_coin) }
                                 |> Seq.map (fun (coin, commemorativeCoin) ->
                                        { Id = coin.id
                                          Type = CommemorativeCoin(country, commemorativeCoin.year, commemorativeCoin.common_issue)
                                          ImageUri = coin.image_uri
                                          NumExtra = coin.num_extra
                                          CollectedAt = coin.collected_at
                                          CollectedBy = coin.collected_by })
                                 |> Seq.toArray
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

module Users =
    let toModel (x : sql.dataContext.``[public].[user]Entity``) =
        { Identifier = x.identifier
          Name = x.name
          Email = x.email
          IsApproved = x.approved
          ProviderName = x.provider_name
          ProviderIdentity = x.provider_identity
          Picture = x.provider_picture
          Roles = x.roles }

    let Create (user : User) =
        db.ClearUpdates() |> ignore
        let dbUser = db.``[public].[user]``.Create(user.IsApproved, user.Email, user.Identifier, user.Picture, user.Roles)
        dbUser.provider_name <- user.ProviderName
        dbUser.provider_identity <- user.ProviderIdentity
        db.SubmitUpdates()
        user

    let GetByIdentity providerName identity =
        query { for user in db.``[public].[user]`` do
                where (user.provider_name = providerName && user.provider_identity = identity) }
        |> Seq.map toModel
        |> Seq.tryFind (fun _ -> true)

    let GetByIdentifier identifier =
        query { for user in db.``[public].[user]`` do
                where (user.identifier = identifier) }
        |> Seq.map toModel
        |> Seq.tryFind (fun _ -> true)

    let Update user =
        let dbUser = query { for u in db.``[public].[user]`` do
                             where (u.provider_name = user.ProviderName && u.provider_identity = user.ProviderIdentity) }
                     |> Seq.tryFind (fun _ -> true)
        match dbUser with
        | Some dbUser ->
            db.ClearUpdates() |> ignore
            dbUser.approved <- user.IsApproved
            dbUser.email <- user.Email
            dbUser.name <- user.Name
            dbUser.provider_picture <- user.Picture
            dbUser.roles <- user.Roles
            db.SubmitUpdates()
        | _ -> ()
        user
