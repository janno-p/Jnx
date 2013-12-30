namespace Jnx.Modules

open Jnx.Modules.Utils
open Nancy

type CoinDetails = {
    CommemorativeYears : string []
    }

type CoinsModule() as this =
    inherit NancyModule()

    let details () =
        { CommemorativeYears = [2004 .. 2014] |> Seq.map (fun x -> x.ToString()) |> Seq.toArray }

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