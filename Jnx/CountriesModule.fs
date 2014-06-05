namespace Jnx.Modules

open Fancy
open Jnx.Modules.Utils
open Jnx.Repositories
open Nancy
open Nancy.ModelBinding
open Nancy.Security
open Nancy.Validation
open System.Collections.Generic
open System.ComponentModel.DataAnnotations

type CountryForm =
    { [<Required(ErrorMessage = "Kood on kohustuslik.")>]
      [<StringLength(2, MinimumLength = 2, ErrorMessage = "Kood peab olema kahetäheline.")>]
      Code : string

      [<Required(ErrorMessage = "Nimi on kohustuslik.")>]
      Name : string

      [<Required(ErrorMessage = "Nime omastav kääne on kohustuslik.")>]
      Genitive : string

      Errors : IDictionary<string, IList<ModelValidationError>> }

    static member Empty =
        { Code = ""; Name = ""; Genitive = ""; Errors = new Dictionary<string, IList<ModelValidationError>>() }

    static member FromModel (country : Country) =
        { CountryForm.Empty with Code = country.Code; Name = country.Name; Genitive = country.Genitive }

    member this.ToModel () : Country =
        { Code = this.Code; Name = this.Name; Genitive = this.Genitive }

type CountryFormBinder () =
    interface IBinder with
        member this.Bind(context, modelType, instance, configuration, blackList) =
            { CountryForm.Empty with Name = context.Request.Form?Name
                                     Code = context.Request.Form?Code
                                     Genitive = context.Request.Form?Genitive } :> obj
    interface IModelBinder with
        member this.CanBind modelType =
            modelType = typeof<CountryForm>

type CountriesModule() as this =
    inherit NancyModule()

    do fancy this {
        before (fun ctx c -> async {
            return match this.Context.CurrentUser with
                   | null -> new Response(StatusCode = HttpStatusCode.NotFound)
                   | _ -> upcast null
        })

        get "/countries" (fun () -> fancyAsync {
            let countries = Countries.GetAll (Range (0, 10))
            return this.View.["Index", countries]
        })

        get "/countries/new" (fun () -> fancyAsync {
            return this.View.["New", CountryForm.Empty]
        })

        get "/countries/(?<code>^[a-z]{2}$)" (fun code -> fancyAsync {
            return match Countries.GetByCode code with
                   | None -> HttpStatusCode.NotFound :> obj
                   | Some country -> this.View.["Show", country] :> obj
        })

        get "/countries/(?<code>^[a-z]{2}$)/edit" (fun code -> fancyAsync {
            return match Countries.GetByCode code with
                   | None -> HttpStatusCode.NotFound :> obj
                   | Some country -> this.View.["Edit", CountryForm.FromModel country] :> obj
        })

        post "/countries" (fun () -> fancyAsync {
            let countryForm = this.BindAndValidate<CountryForm>()
            return match this.ModelValidationResult.IsValid with
                   | true -> countryForm.ToModel() |> Countries.Save |> ignore
                             this.Response.AsRedirect("/countries") :> obj
                   | _ -> this.View.["New", { countryForm with Errors = this.ModelValidationResult.Errors }] :> obj
        })

        put "/countries/(?<code>^[a-z]{2}$)" (fun code -> fancyAsync {
            let countryForm = this.BindAndValidate<CountryForm>()
            return match this.ModelValidationResult.IsValid with
                   | true -> [ ("name", countryForm.Name); ("genitive", countryForm.Genitive) ] |> Countries.Update code |> ignore
                             this.Response.AsRedirect("/countries") :> obj
                   | _ -> this.View.["Edit", { countryForm with Errors = this.ModelValidationResult.Errors }] :> obj
        })

        delete "/countries/(?<code>^[a-z]{2}$)" (fun code -> fancyAsync {
            Countries.Delete code
            return HttpStatusCode.OK
        })
    }
