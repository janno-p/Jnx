namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CoinDetails = { CommemorativeYears : string []
                     Countries : Country []
                     NominalValues : string [] }

type CoinsOfCountry = { Country : Country }
type CommemorativesOfYear = { Year : int
                              Coins : CoinType [] }

type CoinsModule() as this =
    inherit NancyModule()

    let view viewName (viewData : 'T) =
        let data =
            [| { CommemorativeYears = QueryCommemorativeYears () |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
                 Countries = QueryCountries () |> Seq.toArray
                 NominalValues = QueryNominalValues () |> Seq.map (fun x -> x.ToString()) |> Seq.toArray }
               viewData |] : obj []
        this.View.[viewName, data] :> obj

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Alejandro"
        QueryCountryStats () |> Seq.toArray |> view "Index"
    )

    do this.Get.["/coins/(?<countryCode>^[a-z]{2}$)"] <- (fun args ->
        match QueryCountryByCode args?countryCode with
        | Some country -> { Country = country } |> view "Country"
        | _ -> 404 :> obj
    )

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        this.ViewBag?Title <- "Mälestusmündid"
        match QueryCoinsByCommemorativeYear args?year with
        | Some coins -> { Year = unbox<string> args?year |> int; Coins = coins } |> view "Commemorative"
        | _ -> 404 :> obj
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        sprintf "Nominal values of %O" args?nominalValue :> obj
    )