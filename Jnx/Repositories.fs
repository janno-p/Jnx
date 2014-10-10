module Jnx.Repositories

open Npgsql
open System
open System.Data
open System.Configuration
open System.Text

let connectionString = ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString

let openConnection () =
    let connection = new NpgsqlConnection(connectionString)
    connection.Open()
    connection :> IDbConnection

let connectionManager = Sql.withNewConnection(openConnection)
let sql = SqlWrapper(connectionManager)
let exec sqlCommand = sql.ExecNonQuery sqlCommand [] |> ignore
let param = Sql.Parameter.make
let execReader (qry : StringBuilder) = sql.ExecReader (qry.ToString())
let execNonQuery (qry : StringBuilder) = sql.ExecNonQuery (qry.ToString())

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
      CollectedCommon : int64
      CollectedCommemorative : int64
      TotalCommon : int64
      TotalCommemorative : int64 }

type User =
    { Identifier : Guid
      Name : string option
      Email : string
      IsApproved : bool
      ProviderName : string
      ProviderIdentity : string
      Picture : string
      Roles : int }
    static member NewUser with get() = { Identifier = Guid.Empty
                                         Name = None
                                         Email = ""
                                         IsApproved = false
                                         ProviderName = ""
                                         ProviderIdentity = ""
                                         Picture = ""
                                         Roles = 0 }

type Paging =
    | All
    | Range of int * int

let Query (str : string) = StringBuilder(str)
let (@~) (qry : StringBuilder) (str : string) = qry.Append(" ").Append(str)

module Countries =
    let asCountry = Sql.asRecord<Country> ""

    let GetAll paging =
        let qry = Query @"select code as ""Code"", name as ""Name"", genitive as ""Genitive"" from coins_country order by name"
        use reader =
            match paging with
            | All ->
                execReader qry []
            | Range (f, t) ->
                execReader (qry @~ "limit @p1 offset @p2") [param("p1", t - f); param("p2", f)]
        reader
        |> Seq.ofDataReader
        |> Seq.map asCountry
        |> Seq.toArray

    let Save (country : Country) =
        let qry = Query "WITH upsert AS (UPDATE coins_country SET name=@p1, genitive=@p2 WHERE code=@p3 RETURNING *)"
        execNonQuery (qry @~ "INSERT INTO coins_country (code, name, genitive) SELECT @p3, @p1, @p2 WHERE NOT EXISTS (SELECT * FROM upsert)")
                     [param("p1", country.Name); param("p2", country.Genitive); param("p3", country.Code)] |> ignore
        country

    (*
    let Update code columns =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[PUBLIC].[COINS_COUNTRY]`` do where (country.CODE = code) } |> Seq.head
        columns |> Seq.iter (fun (k, v) -> dbCountry.SetColumn(k, v))
        db.SubmitUpdates()
        dbCountry |> ToModel
    *)

    let GetByCode code =
        use reader = sql.ExecReaderF @"select code as ""Code"", name as ""Name"", genitive as ""Genitive"" from coins_country where code = %s" code
        reader |> Sql.mapFirst asCountry

    let Delete code =
        sql.ExecNonQueryF "delete from coins_country where code = %s" code |> ignore

module Coins =
    let asCountryStatistics (dr : #IDataRecord) =
        { Country = { Code = dr?Code.Value; Name = dr?Name.Value; Genitive = dr?Genitive.Value }
          TotalCommon = dr?TotalCommon.Value
          CollectedCommon = dr?CollectedCommon.Value
          TotalCommemorative = dr?TotalCommemorative.Value
          CollectedCommemorative = dr?CollectedCommemorative.Value }

    let asCommonCoin country (dr : #IDataRecord) =
        { Id = dr?Id.Value
          Type = CommonCoin({ Id = dr?CoinageId.Value
                              Year = dr?Year.Value
                              Name = dr?CoinageName.Value
                              Country = country },
                            dr?NominalValue.Value)
          ImageUri = dr?ImageUri.Value
          NumExtra = dr?NumExtra.Value
          CollectedAt = dr?CollectedAt.Value
          CollectedBy = dr?CollectedBy.Value }

    let asCommemorativeCoin country (dr : #IDataRecord) =
        { Id = dr?Id.Value
          Type = CommemorativeCoin(country, dr?Year.Value, dr?CommonIssue.Value)
          ImageUri = dr?ImageUri.Value
          NumExtra = dr?NumExtra.Value
          CollectedAt = dr?CollectedAt.Value
          CollectedBy = dr?CollectedBy.Value }

    let GetCommemorativeYears () =
        use reader = sql.ExecReaderF @"select year from coins_commemorative_coin group by year order by year desc"
        reader |> Seq.ofDataReader |> Seq.map (Sql.asScalar >> unbox<int>) |> Seq.toArray

    let GetNominalValues () =
        use reader = sql.ExecReaderF @"select nominal_value from coins_common_coin group by nominal_value order by nominal_value desc"
        reader |> Seq.ofDataReader |> Seq.map (Sql.asScalar >> unbox<decimal>) |> Seq.toArray

    let GetCountryStatistics () =
        let qry = Query @"select c1.code as ""Code"", c1.name as ""Name"", c1.genitive as ""Genitive"""
        qry @~ @", (select count(1) from coins_coinage c2, coins_common_coin c3 where c2.id = c3.coinage_id and c2.country_code = c1.code) as ""TotalCommon""" |> ignore
        qry @~ @", (select count(1) from coins_coinage c2, coins_common_coin c3, coins_coin c4 where c2.id = c3.coinage_id and c3.coin_id = c4.id and c2.country_code = c1.code and c4.collected_at is not null) as ""CollectedCommon""" |> ignore
        qry @~ @", (select count(1) from coins_commemorative_coin c2 where c2.country_code = c1.code) as ""TotalCommemorative""" |> ignore
        qry @~ @", (select count(1) from coins_commemorative_coin c2, coins_coin c3 where c2.coin_id = c3.id and c2.country_code = c1.code and c3.collected_at is not null) as ""CollectedCommemorative""" |> ignore
        qry @~ @"from coins_country c1 order by c1.name" |> ignore
        use reader = execReader qry []
        reader |> Seq.ofDataReader |> Seq.map asCountryStatistics |> Seq.toArray

    let CommonCoinsOfCountry country =
        let qry = Query @"select c1.id as ""Id"", c1.image_uri as ""ImageUri"", c1.num_extra as ""NumExtra"", c1.collected_at as ""CollectedAt"", c1.collected_by as ""CollectedBy"","
        qry @~ @"c2.nominal_value as ""NominalValue"", c3.id as ""CoinageId"", c3.year as ""Year"", c3.name as ""CoinageName"" from coins_coinage c3" |> ignore
        qry @~ "inner join coins_common_coin c2 on c2.coinage_id = c3.id" |> ignore
        qry @~ "inner join coins_coin c1 on c1.id = c2.coin_id" |> ignore
        qry @~ "where c3.country_code = @p1" |> ignore
        qry @~ "order by c2.nominal_value desc" |> ignore
        use reader = execReader qry [param("p1", country.Code)]
        reader |> Seq.ofDataReader |> Seq.map (asCommonCoin country) |> Seq.toArray

    let CommemorativeCoinsOfCountry country =
        let qry = Query @"select c1.id as ""Id"", c1.image_uri as ""ImageUri"", c1.num_extra as ""NumExtra"", c1.collected_at as ""CollectedAt"", c1.collected_by as ""CollectedBy"","
        qry @~ @"c2.year as ""Year"", c2.common_issue as ""CommonIssue"" from coins_commemorative_coin c2" |> ignore
        qry @~ "inner join coins_coin c1 on c1.id = c2.coin_id" |> ignore
        qry @~ "where c2.country_code = @p1" |> ignore
        qry @~ "order by c2.year desc" |> ignore
        use reader = execReader qry [param("p1", country.Code)]
        reader |> Seq.ofDataReader |> Seq.map (asCommemorativeCoin country) |> Seq.toArray

    let OfCountry country =
        (CommonCoinsOfCountry country, CommemorativeCoinsOfCountry country)

    let OfCommemorativeYear year =
        let qry = Query @"select c1.id as ""Id"", c1.image_uri as ""ImageUri"", c1.num_extra as ""NumExtra"", c1.collected_at as ""CollectedAt"", c1.collected_by as ""CollectedBy"","
        qry @~ @"c3.code as ""Code"", c3.name as ""Name"", c3.genitive as ""Genitive""," |> ignore
        qry @~ @"c2.year as ""Year"", c2.common_issue as ""CommonIssue"" from coins_commemorative_coin c2" |> ignore
        qry @~ "inner join coins_coin c1 on c1.id = c2.coin_id" |> ignore
        qry @~ "inner join coins_country c3 on c3.code = c2.country_code" |> ignore
        qry @~ "where c2.year = @p1" |> ignore
        qry @~ "order by c3.name, c1.id" |> ignore
        use reader = execReader qry [param("p1", year)]
        reader |> Seq.ofDataReader |> Seq.map (fun dr -> asCommemorativeCoin (Countries.asCountry dr) dr) |> Seq.toArray

    let OfNominalValue nominalValue =
        let qry = Query @"select c1.id as ""Id"", c1.image_uri as ""ImageUri"", c1.num_extra as ""NumExtra"", c1.collected_at as ""CollectedAt"", c1.collected_by as ""CollectedBy"","
        qry @~ @"c4.code as ""Code"", c4.name as ""Name"", c4.genitive as ""Genitive""," |> ignore
        qry @~ @"c2.nominal_value as ""NominalValue"", c3.id as ""CoinageId"", c3.year as ""Year"", c3.name as ""CoinageName"" from coins_coinage c3" |> ignore
        qry @~ "inner join coins_common_coin c2 on c2.coinage_id = c3.id" |> ignore
        qry @~ "inner join coins_coin c1 on c1.id = c2.coin_id" |> ignore
        qry @~ "inner join coins_country c4 on c4.code = c3.country_code" |> ignore
        qry @~ "where c2.nominal_value = @p1" |> ignore
        qry @~ "order by c4.name" |> ignore
        use reader = execReader qry [param("p1", nominalValue)]
        reader |> Seq.ofDataReader |> Seq.map (fun dr -> asCommonCoin (Countries.asCountry dr) dr) |> Seq.toArray

    let GetById id =
        let qry = Query @"select coin.id as ""Id"", coin.image_uri as ""ImageUri"", coin.num_extra as ""NumExtra"", coin.collected_at as ""CollectedAt"", coin.collected_by as ""CollectedBy"","
        qry @~ @"common.nominal_value as ""NominalValue""," |> ignore
        qry @~ @"comm.year as ""Year"", comm.common_issue as ""CommonIssue""," |> ignore
        qry @~ @"coinage.id as ""CoinageId"", coinage.year as ""CoinageYear"", coinage.name as ""CoinageName""," |> ignore
        qry @~ @"country.code as ""Code"", country.name as ""Name"", country.genitive as ""Genitive""" |> ignore
        qry @~ @"from coins_coin coin" |> ignore
        qry @~ @"left join coins_common_coin common on common.coin_id = coin.id" |> ignore
        qry @~ @"left join coins_commemorative_coin comm on comm.coin_id = coin.id" |> ignore
        qry @~ @"left join coins_coinage coinage on coinage.id = common.coinage_id" |> ignore
        qry @~ @"left join coins_country country on country.code = coinage.country_code or country.code = comm.country_code" |> ignore
        qry @~ "where coin.id = @p1" |> ignore
        use reader = execReader qry [param("p1", id)]
        reader |> Sql.mapFirst (fun dr -> match dr?CoinageId with
                                          | Some n -> asCommonCoin (Countries.asCountry dr) dr
                                          | _ -> asCommemorativeCoin (Countries.asCountry dr) dr)

module Users =
    let asUser (dr : #IDataRecord) =
        { Identifier = dr?UserIdentifier.Value
          Name = dr?UserName.Value
          Email = dr?UserEmail.Value
          IsApproved = dr?UserApproved.Value
          ProviderName = dr?UserProviderName.Value
          ProviderIdentity = dr?UserProviderIdentity.Value
          Picture = dr?UserProviderPicture.Value
          Roles = dr?UserRoles.Value }

    let userColumns prefix =
        let prefix = match prefix with | Some p -> sprintf "%s." p | _ -> ""
        StringBuilder().AppendFormat(@"{0}identifier as ""UserIdentifier"",", prefix)
                       .AppendFormat(@"{0}name as ""UserName"",", prefix)
                       .AppendFormat(@"{0}email as ""UserEmail"",", prefix)
                       .AppendFormat(@"{0}approved as ""UserApproved"",", prefix)
                       .AppendFormat(@"{0}provider_picture as ""UserProviderPicture"",", prefix)
                       .AppendFormat(@"{0}roles as ""UserRoles"",", prefix)
                       .AppendFormat(@"{0}provider_name as ""UserProviderName"",", prefix)
                       .AppendFormat(@"{0}provider_identity as ""UserProviderIdentity""", prefix)
                       .ToString()

    let Save (user : User) =
        let user, qry =
            match user.Identifier with
            | id when id = Guid.Empty -> { user with Identifier = Guid.NewGuid() }, "INSERT INTO user (identifier, name, email, approved, provider_name, provider_identity, provider_picture, roles) VALUES (@p1, @p2, @p3, @p4, @p7, @p8, @p5, @p6)"
            | _ -> user, "UPDATE user SET identifier=@p1, name=@p2, email=@p3, approved=@p4, provider_picture=@p5, roles=@p6 WHERE provider_name=@p7 and provider_identity=@p8"
        sql.ExecNonQuery qry [param("p1", user.Identifier)
                              param("p2", user.Name)
                              param("p3", user.Email)
                              param("p4", user.IsApproved)
                              param("p5", user.Picture)
                              param("p6", user.Roles)
                              param("p7", user.ProviderName)
                              param("p8", user.ProviderIdentity)] |> ignore
        user

    let GetByIdentity (providerName: string) (identity: string) =
        let qry = sprintf "SELECT %s FROM user WHERE provider_name=@p1 AND provider_identity=@p2" (userColumns None)
        use reader = sql.ExecReader qry [param("p1", providerName); param("p2", identity)]
        reader |> Sql.mapFirst asUser

    let GetByIdentifier (identifier: Guid) =
        let qry = sprintf @"SELECT %s FROM user WHERE identifier=@p1" (userColumns None)
        use reader = sql.ExecReader qry [param("p1", identifier)]
        reader |> Sql.mapFirst asUser
