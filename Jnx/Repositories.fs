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

module Countries =
    let ToModel (country : sql.dataContext.``[public].[coins_country]Entity``) =
        { Id = country.id; Code = country.code; Name = country.name; Genitive = country.genitive }

    //let ToModelOption (country : sql.dataContext.``[public].[coins_country]Entity``) =
    //    match box country with
    //    | null -> None
    //    | _ -> Some (ToModel country)

    let GetAll () =
        query { for country in db.``[public].[coins_country]`` do
                sortBy country.name
                select { Id = country.id; Code = country.code; Name = country.name; Genitive = country.genitive } } |> Seq.toArray

    let Save (country : Country) =
        db.ClearUpdates() |> ignore
        let dbCountry = db.``[public].[coins_country]``.Create(country.Code, country.Genitive, country.Name)
        db.SubmitUpdates()
        { country with Id = dbCountry.id }

    let GetByCode code =
        query { for country in db.``[public].[coins_country]`` do
                where (country.code = code)
        //        exactlyOneOrDefault } |> ToModelOption
        } |> Seq.map ToModel |> Seq.tryFind (fun _ -> true)
