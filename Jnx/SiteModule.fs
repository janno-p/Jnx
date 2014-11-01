namespace Jnx.Modules

open Fancy
open Jnx.Modules.Utils
open Nancy
open Nancy.Authentication.Forms

type SiteModule() as this =
    inherit NancyModule()

    do fancy this {
        get "/" (fun () -> fancyAsync {
            this.ViewBag ? Title <- "Jnx"
            return this.View.["Index"]
        })

        get "/logout" (fun () -> fancyAsync {
            return this.LogoutAndRedirect("/")
        })

        get "/admin" (fun () -> fancyAsync {
            return this.View.["Admin/Index"]
        })
    }
