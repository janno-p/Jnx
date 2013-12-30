namespace Jnx.Modules

open Jnx.Modules.Utils
open Nancy

type BlogsModule() as this =
    inherit NancyModule()

    do this.Get.["/bla"] <- (fun _ ->
        this.ViewBag?Title <- "Bla"
        this.View.["Index"] :> obj
    )
