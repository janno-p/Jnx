module Jnx.NancyExtensions

open Nancy
open System.Collections.Generic

type AlertMessageStore () =
    let store = Dictionary<string, string>()
    member this.AddMessage name message =
        store.[name] <- message
    member this.GetMessage name =
        match store.TryGetValue name with
        | true, value -> value
        | _ -> ""

let (?) (store : AlertMessageStore) name =
    store.GetMessage name

let (?<-) (store : AlertMessageStore) name message =
    store.AddMessage name message

type NancyModule with
    member this.Flash name message =
        let store = match this.Session.["flash"] with
                    | :? AlertMessageStore as store -> store
                    | _ -> let store = AlertMessageStore()
                           this.Session.["flash"] <- store
                           store
        store?name <- message
