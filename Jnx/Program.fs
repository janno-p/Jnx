module Jnx.Main

open Jnx.NancyExtensions
open Nancy
open Nancy.Authentication.Forms
open Nancy.Conventions
open Nancy.Diagnostics
open Nancy.Hosting.Self
open Nancy.Session
open System
open System.Collections.Generic
open System.Threading

type RunningMode = Daemon | Application

type CommandLineOptions = {
    RunningMode : RunningMode
    Port : int
    }

let parseCommandLine args =
    let defaultOptions = { RunningMode = Application; Port = 1234 }
    let rec parseCommandLineRec args options =
        match args with
        | [] -> options
        | "/D"::xs ->
            let newOptions = { options with RunningMode = Daemon }
            parseCommandLineRec xs newOptions
        | "/P"::xs ->
            match xs with
            | [] ->
                eprintfn "Port number not set."
                options
            | p::xss ->
                let port = int p
                let newOptions = { options with Port = port }
                parseCommandLineRec xss newOptions
        | x::xs ->
            eprintfn "Option '%s' is unrecognized" x
            parseCommandLineRec xs options
    parseCommandLineRec (args |> Seq.toList) defaultOptions

type Bootstrapper() =
    inherit DefaultNancyBootstrapper()

    override this.ApplicationStartup (_, pipelines) =
        CookieBasedSessions.Enable(pipelines) |> ignore
        SessionFlashStore.Enable(pipelines)
        DiagnosticsHook.Disable(pipelines)

    override this.ConfigureConventions conventions =
        base.ConfigureConventions conventions
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("Scripts", "Scripts", "js"))
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("fonts", "fonts", "eot", "svg", "ttf", "woff"))

    override this.ConfigureRequestContainer (container, context) =
        base.ConfigureRequestContainer (container, context)
        container.Register<IUserMapper, Jnx.Authentication.DatabaseUser>() |> ignore

    override this.RequestStartup (container, pipelines, context) =
        base.RequestStartup (container, pipelines, context)
        let configuration = FormsAuthenticationConfiguration(RedirectUrl = "~/login", UserMapper = container.Resolve<IUserMapper>())
        FormsAuthentication.Enable(pipelines, configuration)

[<EntryPoint>]
let main args =
    let options = parseCommandLine args

    let uriString = sprintf "http://localhost:%d" options.Port
    printfn "Starting self hosting on %s" uriString

    use host = new NancyHost(new Uri(uriString))
    host.Start()

    match options.RunningMode with
    | Daemon -> Thread.Sleep(Timeout.Infinite)
    | _ -> Console.ReadLine() |> ignore

    host.Stop()

    0
