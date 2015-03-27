open Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Files
open Suave.Http.Successful
open Suave.Http.RequestErrors
open Suave.Razor
open Suave.Types
open Suave.Web
open System
open System.Reflection
open System.Security
open System.Security.Permissions

let mimeTypesMap = function
    | ".css" -> Some { name = "text/css"; compression = true }
    | ".js" -> Some { name = "application/x-javascript"; compression = true }
    | _ -> None

let app =
    choose
      [ GET >>= choose
          [ path "/hello" >>= OK "Hello GET"
            path "/goodbye" >>= OK "Good bye GET"
            path "/home" >>= razor "Views/Site/Home.cshtml" None
            path "/x" >>= (fun r -> async { return! OK (sprintf "%s" r.runtime.homeDirectory) r }) ]
        GET >>= browseHome
        POST >>= choose
          [ path "/hello" >>= OK "Hello POST"
            path "/goodbye" >>= OK "Good bye POST" ]
        NOT_FOUND "Found no handlers" ]

[<EntryPoint>]
let main argv =
    if AppDomain.CurrentDomain.IsDefaultAppDomain() then
        printfn "Switching to second AppDomain, for RazorEngine..."
        let setup = AppDomainSetup()
        setup.ApplicationBase <- AppDomain.CurrentDomain.SetupInformation.ApplicationBase
        let domain = AppDomain.CreateDomain(
                        "MyMainDomain",
                        null,
                        AppDomain.CurrentDomain.SetupInformation,
                        PermissionSet(PermissionState.Unrestricted),
                        [| |])
        let exitCode = domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location)
        AppDomain.Unload(domain)
        exitCode
    else
        printfn "%A" defaultConfig.homeFolder
        app |> startWebServer { defaultConfig with mimeTypesMap = mimeTypesMap }
        0
