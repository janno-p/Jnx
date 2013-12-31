namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CoinDetails = { CommemorativeYears : string []
                     Countries : Country []
                     NominalValues : string [] }

type CoinsModule() as this =
    inherit NancyModule()

    let commemorativeYears = [2004 .. 2014] |> Seq.map (fun x -> x.ToString()) |> Seq.toArray
    let countries = [|
        { Id = 1; Code = "et"; Name = "Eesti"; Genitive = "Eesti" }
        { Id = 2; Code = "at"; Name = "Austria"; Genitive = "Austria" }
        { Id = 3; Code = "lv"; Name = "Läti"; Genitive = "Läti" }
    |]
    let nominalValues = [| "2.00"; "1.00"; "0.50"; "0.20"; "0.10"; "0.05"; "0.02"; "0.01" |]

    let details () = { CommemorativeYears = commemorativeYears; Countries = countries; NominalValues = nominalValues }

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Alejandro"
        this.View.["Index", details()] :> obj
    )

    do this.Get.["/coins/(?<countryCode>^[a-z]{2}$)"] <- (fun args ->
        sprintf "Country Code: %O" args?countryCode :> obj
    )

    do this.Get.["/coins/(?<year>^\d{4}$)"] <- (fun args ->
        sprintf "Year of %O" args?year :> obj
    )

    do this.Get.["/coins/(?<nominalValue>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        sprintf "Nominal values of %O" args?nominalValue :> obj
    )