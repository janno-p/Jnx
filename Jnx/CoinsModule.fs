namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CoinDetails = { CommemorativeYears : string []
                     Countries : Country []
                     NominalValues : string [] }

type CountryStats =
    { Country : Country
      CollectedCommon : int
      CollectedCommemorative : int
      TotalCommon : int
      TotalCommemorative : int }
    member x.CommonPercent with get () = match x.TotalCommon with | 0 -> 100 | _ -> x.CollectedCommon * 100 / x.TotalCommon
    member x.CommemorativePercent with get () = match x.TotalCommemorative with | 0 -> 100 | _ -> x.CollectedCommemorative * 100 / x.TotalCommemorative

type CommemorativesOfYear = { Year : int }

type CoinsModule() as this =
    inherit NancyModule()

    let commemorativeYears = [2004 .. 2014] |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
    let countries = [|
        { Id = 1; Code = "et"; Name = "Eesti"; Genitive = "Eesti" }
        { Id = 2; Code = "at"; Name = "Austria"; Genitive = "Austria" }
        { Id = 3; Code = "lv"; Name = "Läti"; Genitive = "Läti" }
    |]
    let nominalValues = [| "2.00"; "1.00"; "0.50"; "0.20"; "0.10"; "0.05"; "0.02"; "0.01" |]

    let view viewName (viewData : 'T) =
        let data =
            [|
                { CommemorativeYears = commemorativeYears; Countries = countries; NominalValues = nominalValues }
                viewData
            |] : obj []
        this.View.[viewName, data] :> obj

    let loadCountryStats () =
        [|
            { Country = countries.[0]; CollectedCommon = 8; CollectedCommemorative = 1; TotalCommon = 8; TotalCommemorative = 1 }
            { Country = countries.[1]; CollectedCommon = 8; CollectedCommemorative = 2; TotalCommon = 8; TotalCommemorative = 7 }
            { Country = countries.[2]; CollectedCommon = 0; CollectedCommemorative = 0; TotalCommon = 8; TotalCommemorative = 0 }
        |]

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Alejandro"
        loadCountryStats () |> view "Index"
    )

    do this.Get.["/coins/(?<countryCode>^[a-z]{2}$)"] <- (fun args ->
        sprintf "Country Code: %O" args?countryCode :> obj
    )

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        this.ViewBag?Title <- "Mälestusmündid"
        { Year = unbox<string> args?year |> int } |> view "Commemorative"
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        sprintf "Nominal values of %O" args?nominalValue :> obj
    )