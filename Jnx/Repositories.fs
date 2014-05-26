module Jnx.Repositories

open FSharp.Data.Sql
open System.Configuration

type sql = SqlDataProvider<"Server=127.0.0.1; Port=5432; Database=Jnx; User Id=Jnx; Password=Jnx;",
                           Common.DatabaseProviderTypes.POSTGRESQL,
                           @"/opt/mono-3.4.0/lib/mono/4.5",
                           100,
                           true>

let db = sql.GetDataContext(ConfigurationManager.ConnectionStrings.["Jnx"].ConnectionString)

type Country = { Id : int
                 Code : string
                 Name : string
                 Genitive : string }

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
        { Id = country.id; Code = country.code; Name = country.name; Genitive = country.genitive }

    let GetAll paging =
        let baseQuery = pquery { for country in db.``[public].[coins_country]`` do
                                 sortBy country.name }
        let pagingQuery = match paging with
                          | All -> baseQuery
                          | Range (f, t) -> pquery { for country in %baseQuery do
                                                     skip f
                                                     take (t - f) }
        query { for country in %pagingQuery do
                select { Id = country.id
                         Code = country.code
                         Name = country.name
                         Genitive = country.genitive } } |> Seq.toArray

    //let ToModelOption (country : sql.dataContext.``[public].[coins_country]Entity``) =
    //    match box country with
    //    | null -> None
    //    | _ -> Some (ToModel country)

    let Save (country : Country) =
        db.ClearUpdates() |> ignore
        let dbCountry = db.``[public].[coins_country]``.Create(country.Code, country.Genitive, country.Name)
        db.SubmitUpdates()
        { country with Id = dbCountry.id }

    let Update id columns =
        db.ClearUpdates() |> ignore
        let dbCountry = query { for country in db.``[public].[coins_country]`` do where (country.id = id) } |> Seq.head
        dbCountry.SetData(columns)
        db.SubmitUpdates()
        dbCountry |> ToModel

    let GetByCode code =
        query { for country in db.``[public].[coins_country]`` do where (country.code = code) }
        |> Seq.map ToModel
        |> Seq.tryFind (fun _ -> true)
