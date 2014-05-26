namespace Jnx.Modules

open Fancy
open Jnx.Modules.Utils
open Nancy

type BlogsModule() as this =
    inherit NancyModule()

    do fancy this {
        get "/bla" (fun () -> fancyAsync {
            this.ViewBag ? Title <- "Bla"
            return this.View.["Index"] :> obj
        })
    }
