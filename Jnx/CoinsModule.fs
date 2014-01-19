namespace Jnx.Modules

open Jnx.Database.Types
open Jnx.Database.Queries
open Jnx.Modules.Utils
open Nancy

type LayoutDetails =
    { CommemorativeYears : string []
      Countries : Country []
      NominalValues : decimal [] }

type ViewData<'T> =
    { LayoutDetails : LayoutDetails
      ViewDetails : 'T }

type CoinsOfCountry = { Country : Country
                        CommonCoins : Coin []
                        CommemorativeCoins : Coin [] }

type CoinsWithNominalValue = { NominalValue : decimal
                               Coins : Coin [] }

type CommemorativesOfYear = { Year : int
                              Coins : Coin [] }

type CoinsModule() as this =
    inherit NancyModule()

    let loadLayoutDetails () =
        { CommemorativeYears = QueryCommemorativeYears() |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
          Countries = QueryCountries() |> Seq.toArray
          NominalValues = QueryNominalValues() |> Seq.toArray }

    let view viewName (viewData : 'T) =
        let model = { LayoutDetails = loadLayoutDetails()
                      ViewDetails = viewData }
        this.View.[viewName, model] :> obj

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

    do this.Get.["/coins/(?<id>^\d+$)/edit"] <- (fun args ->
        let id = unbox<string> args?id |> int
        match QueryCoinById id with
        | Some coin -> coin |> view "Edit"
        | _ -> notFound
    )

    do this.Post.["/coins/(?<id>^\d+$)"] <- (fun args ->
        let id = unbox<string> args?id |> int
        match QueryCoinById id with
        | Some coin ->
            let coin =
                { coin with
                    CollectedBy = match unbox this.Request.Form?CoinCollectedBy with
                                  | null | "" -> None
                                  | x -> Some x
                    CollectedAt = match unbox this.Request.Form?CoinCollectedAt with
                                  | null | "" -> None
                                  | x -> let mutable dt = System.DateTime.MinValue
                                         match System.DateTime.TryParseExact(x, "dd.MM.yyyy HH:mm:ss", null, System.Globalization.DateTimeStyles.None, &dt) with
                                         | true -> Some dt
                                         | _ -> None
                    ForTrade = match unbox this.Request.Form?CoinForTrade with
                               | null | "" -> 0
                               | x -> let mutable i = 0
                                      match System.Int32.TryParse(x, &i) with
                                      | true -> i
                                      | _ -> 0 }
            UpdateCoin coin |> ignore
        | None -> ()
        this.Response.AsRedirect(sprintf "/coins/%d/edit" id) :> obj
    )
