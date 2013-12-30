namespace Jnx.Modules

open Jnx.Database
open Jnx.Modules.Utils
open Nancy

type CountriesModule() as this = 
    inherit NancyModule()

    let view path model =
        this.View.[path, model] :> obj

    do this.Get.["/countries"] <- (fun _ ->
        Query toCountry "SELECT * FROM Countries" |> view "Index"
    )