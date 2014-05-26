namespace Jnx.Modules

open Fancy
open Jnx.Modules.Utils
open Nancy

type SiteModule() as this =
    inherit NancyModule()

    do fancy this {
        get "/" (fun () -> fancyAsync {
            this.ViewBag ? Title <- "Jnx"
            return this.View.["Index"] :> obj
        })
    }
