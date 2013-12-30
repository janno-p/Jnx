namespace Jnx.Modules

open Jnx.Modules.Utils
open Nancy

type SiteModule() as this =
    inherit NancyModule()

    do this.Get.["/"] <- (fun _ ->
        this.ViewBag?Title <- "Jnx"
        this.View.["Index"] :> obj
    )
