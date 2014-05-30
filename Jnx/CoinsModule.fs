namespace Jnx.Modules

open Fancy
open Jnx.Modules.Utils
open Jnx.Repositories
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
        { CommemorativeYears = Coins.GetCommemorativeYears() |> Array.map (fun x -> x.ToString())
          Countries = Countries.GetAll All
          NominalValues = Coins.GetNominalValues() }

    let view viewName (viewData : 'T) =
        let model = { LayoutDetails = loadLayoutDetails()
                      ViewDetails = viewData }
        this.View.[viewName, model] :> obj

    do fancy this {
        get "/coins" (fun () -> fancyAsync {
            this.ViewBag ? Title <- "Mündikogu"
            return Coins.GetCountryStatistics() |> view "Index"
        })

        get "/coins/(?<countryCode>^[a-z]{2}$)" (fun countryCode -> fancyAsync {
            return match Countries.GetByCode countryCode with
                   | Some country ->
                        let commonCoins, commemorativeCoins = Coins.OfCountry country
                        this.ViewBag ? Title <- sprintf "%s mündid" country.Genitive
                        { Country = country; CommonCoins = commonCoins; CommemorativeCoins = commemorativeCoins }
                            |> view "Country"
                   | _ -> 404 :> obj
        })
    }





    let notFound = 404 :> obj

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        let year = unbox<string> args?year |> int
        match Coins.OfCommemorativeYear (int16 year) with
        | [||] -> notFound
        | coins ->
            this.ViewBag?Title <- sprintf "Mälestusmündid aastast %d" year
            { Year = year |> int; Coins = coins } |> view "Commemorative"
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        let nominalValue = unbox<string> args?nominalValue |> decimal
        match Coins.OfNominalValue nominalValue with
        | [||] -> notFound
        | coins ->
            this.ViewBag?Title <- sprintf "Euromündid väärtusega €%.2M" nominalValue
            { NominalValue = nominalValue; Coins = coins } |> view "Nominal"
    )

    do this.Get.["/coins/(?<id>^\d+$)/edit"] <- (fun args ->
        let id = unbox<string> args?id |> int
        match Coins.GetById id with
        | Some coin -> coin |> view "Edit"
        | _ -> notFound
    )

    do this.Post.["/coins/(?<id>^\d+$)"] <- (fun args ->
        let id = unbox<string> args?id |> int
        match Coins.GetById id with
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
                    NumExtra = match unbox this.Request.Form?CoinForTrade with
                               | null | "" -> 0s
                               | x -> let mutable i = 0s
                                      match System.Int16.TryParse(x, &i) with
                                      | true -> i
                                      | _ -> 0s }
            //UpdateCoin coin |> ignore
            ()
        | None -> ()
        this.Response.AsRedirect(sprintf "/coins/%d/edit" id) :> obj
    )
