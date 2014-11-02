namespace Jnx.Modules

open Fancy
open Nancy

type AdminModule() as this =
    inherit NancyModule()

    do fancy this {
        before (fun ctx c -> async {
            return match this.Context.CurrentUser with
                   | null -> new Response(StatusCode = HttpStatusCode.NotFound)
                   | _ -> upcast null
        })

        get "/admin" (fun () -> fancyAsync {
            return this.View.["Index"]
        })
    }
