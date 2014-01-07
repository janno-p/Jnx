module Jnx.Main

open Nancy
open Nancy.Conventions
open Nancy.Hosting.Self
open System
open System.Threading

type RunningMode = Daemon | Application | Migrate | InitDatabase

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
        | "/M"::xs ->
            let newOptions = { options with RunningMode = Migrate }
            parseCommandLineRec xs newOptions
        | "/I"::xs ->
            let newOptions = { options with RunningMode = InitDatabase }
            parseCommandLineRec xs newOptions
        | x::xs ->
            eprintfn "Option '%s' is unrecognized" x
            parseCommandLineRec xs options
    parseCommandLineRec (args |> Seq.toList) defaultOptions

type Bootstrapper() =
    inherit DefaultNancyBootstrapper()

    override this.ConfigureConventions conventions =
        base.ConfigureConventions conventions
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("Scripts", "Scripts", "js"))

[<EntryPoint>]
let main args =
    let options = parseCommandLine args

    match options.RunningMode with
    | InitDatabase -> Jnx.DbMigration.Runner.InitDatabase()
    | Migrate -> Jnx.DbMigration.Runner.RunMigrations()
    | _ ->
        let uriString = sprintf "http://localhost:%d" options.Port
        printfn "Starting self hosting on %s" uriString

        use host = new NancyHost(new Uri(uriString))
        host.Start()

        match options.RunningMode with
        | Daemon -> Thread.Sleep(Timeout.Infinite)
        | _ -> Console.ReadLine() |> ignore

        host.Stop()

    0
