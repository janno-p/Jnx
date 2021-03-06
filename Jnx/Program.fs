module Jnx.Main

open Nancy.Hosting.Self
open System
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
