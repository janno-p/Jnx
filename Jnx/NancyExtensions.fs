module Jnx.NancyExtensions

open Nancy
open Nancy.Bootstrapper
open Nancy.Session
open Newtonsoft.Json
open System
open System.Collections.Generic

type FlashStore = Dictionary<string, string>

[<Literal>]
let FlashStoreKey = "Jnx-Flash-0098581e-f9b8-4d11-bbfa-b5da6cbea2a0"

type ISession with
    member this.GetValue<'T> key =
        if String.IsNullOrWhiteSpace key then
            failwith "key must have a value"
        match this.[key] with
        | null -> None
        | value -> Some (JsonConvert.DeserializeObject<'T>(value.ToString()))

    member this.SetValue key value =
        match String.IsNullOrWhiteSpace key with
        | true -> failwith "key must have a value"
        | _ -> this.[key] <- JsonConvert.SerializeObject(value)

type NancyModule with
    member this.Flash name message =
        let store = match this.Session.GetValue<FlashStore> FlashStoreKey with
                    | Some store -> store
                    | None -> FlashStore()
        store.[name] <- message
        this.Session.SetValue FlashStoreKey store

type SessionFlashStore () =
    static member Enable (pipelines : IPipelines) =
        pipelines.BeforeRequest.AddItemToEndOfPipeline((fun (ctx : NancyContext) ->
            match ctx.Request.Session.GetValue<FlashStore> FlashStoreKey with
            | Some store -> ctx.Items.[FlashStoreKey] <- store
                            ctx.Request.Session.Delete FlashStoreKey
            | _ -> ()
            null
        ))
