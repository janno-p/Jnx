namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CoinDetails = { CommemorativeYears : string []
                     Countries : Country []
                     NominalValues : decimal [] }

type CoinsOfCountry = { Country : Country
                        CommonCoins : CommonCoin []
                        CommemorativeCoins : CommemorativeCoin [] }

type CoinsWithNominalValue = { NominalValue : decimal
                               Coins : CommonCoin [] }

type CommemorativesOfYear = { Year : int
                              Coins : CommemorativeCoin [] }

type CoinsModule() as this =
    inherit NancyModule()

    let view viewName (viewData : 'T) =
        let data =
            [| { CommemorativeYears = QueryCommemorativeYears () |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
                 Countries = QueryCountries () |> Seq.toArray
                 NominalValues = QueryNominalValues () |> Seq.toArray }
               viewData |] : obj []
        this.View.[viewName, data] :> obj
    let notFound = 404 :> obj

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Mündikogu"
        QueryCountryStats () |> Seq.toArray |> view "Index"
    )

    do this.Get.["/coins/(?<countryCode>^[a-z]{2}$)"] <- (fun args ->
        let countryCode = unbox<string> args?countryCode
        match QueryCountryByCode countryCode with
        | Some country ->
            let commonCoins, commemorativeCoins = QueryCoinsOfCountry country
            this.ViewBag?Title <- sprintf "%s mündid" country.Genitive
            { Country = country
              CommonCoins = commonCoins |> Seq.toArray
              CommemorativeCoins = commemorativeCoins |> Seq.toArray }
            |> view "Country"
        | _ -> notFound
    )

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        let year = unbox<string> args?year |> int
        match QueryCoinsByCommemorativeYear year |> Seq.toArray with
        | [||] -> notFound
        | coins ->
            this.ViewBag?Title <- sprintf "Mälestusmündid aastast %d" year
            { Year = year |> int; Coins = coins } |> view "Commemorative"
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        let nominalValue = unbox<string> args?nominalValue |> decimal
        match QueryCoinsByNominalValue nominalValue |> Seq.toArray with
        | [||] -> notFound
        | coins ->
            this.ViewBag?Title <- sprintf "Euromündid väärtusega €%.2M" nominalValue
            { NominalValue = nominalValue; Coins = coins } |> view "Nominal"
    )
