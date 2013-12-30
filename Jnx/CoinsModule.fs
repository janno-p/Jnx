namespace Jnx.Modules

open Jnx.Modules.Utils
open Nancy

type CoinsModule() as this =
    inherit NancyModule()

    do this.Get.["/coins"] <- (fun _ ->
        this.ViewBag?Title <- "Alejandro"
        this.View.["Index"] :> obj
    )

    do this.Get.["/coins/country/(?<code>^[a-z]{2}$)"] <- (fun args ->
        sprintf "Country Code: %O" args?code :> obj
    )

    do this.Get.["/coins/year/(?<year>^\d{4}$)"] <- (fun args ->
        sprintf "Year of %O" args?year :> obj
    )

    do this.Get.["/coins/nominal/(?<value>(^[12]\.00$)|(^0\.[125]0$)|(^0\.0[125]$))"] <- (fun args ->
        sprintf "Nominal values of %O" args?value :> obj
    )