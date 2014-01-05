namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CoinDetails = { CommemorativeYears : string []
                     Countries : Country []
                     NominalValues : string [] }

type CoinsOfCountry = { Country : Country
                        CommonCoins : CommonCoin []
                        CommemorativeCoins : CommemorativeCoin [] }

type CommemorativesOfYear = { Year : int
                              Coins : CommemorativeCoin [] }

type CoinsModule() as this =
    inherit NancyModule()

    let view viewName (viewData : 'T) =
        let data =
            [| { CommemorativeYears = QueryCommemorativeYears () |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
                 Countries = QueryCountries () |> Seq.toArray
                 NominalValues = QueryNominalValues () |> Seq.map (fun x -> x.ToString()) |> Seq.toArray }
               viewData |] : obj []
        this.View.[viewName, data] :> obj
    let notFound = 404 :> obj

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Alejandro"
        QueryCountryStats () |> Seq.toArray |> view "Index"
    )

    do this.Get.["/coins/(?<countryCode>^[a-z]{2}$)"] <- (fun args ->
        match QueryCountryByCode args?countryCode with
        | Some country ->
            this.ViewBag?Title <- sprintf "%s mündid" country.Genitive
            { Country = country; CommonCoins = [||]; CommemorativeCoins = [||] } |> view "Country"
        | _ -> notFound
    )

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        let year = unbox<string> args?year |> int
        match QueryCoinsByCommemorativeYear year with
        | Some coins ->
            this.ViewBag?Title <- sprintf "Mälestusmündid aastast %d" year
            { Year = year |> int; Coins = coins } |> view "Commemorative"
        | _ -> notFound
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        sprintf "Nominal values of %O" args?nominalValue :> obj
    )
