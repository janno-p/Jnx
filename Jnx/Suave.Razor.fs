namespace Suave

open System
open System.IO
open Suave.Types
open Suave.Types.Codes
open Suave.Http
open Suave.Http.Files
open Suave.Utils

open RazorEngine
open RazorEngine.Configuration
open RazorEngine.Templating

module Razor =

    let private memoize f =
        let cache = Collections.Concurrent.ConcurrentDictionary<_,_>()
        fun x ->
            match cache.TryGetValue(x) with
            | true, res -> res
            | _ ->
                let res = f x
                cache.[x] <- res
                res

    let private loadTemplate path =
        use file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        use reader = new StreamReader(file)
        reader.ReadToEnd()

    let loadTemplateCached = memoize loadTemplate

    type private MyTemplateManager() =
        interface ITemplateManager with
            override __.Resolve(key) =
                upcast LoadedTemplateSource(loadTemplateCached key.Name)
            override __.GetKey(name, resolveType, context) =
                upcast NameOnlyTemplateKey(name, resolveType, context)
            override __.AddDynamic(key, source) =
                ()

    let razor<'a> path (model : 'a) =
        fun r -> async {
            try
                let s = loadTemplateCached path
                let content = Engine.Razor.RunCompile(s, path, null, model)
                return! Response.response HTTP_200 (UTF8.bytes content) r
            with 
                ex ->
                return! Response.response HTTP_500 (UTF8.bytes (ex.ToString())) r
        }

    Engine.Razor <- RazorEngineService.Create(
                        TemplateServiceConfiguration(
                            Debug = true,
                            TemplateManager = MyTemplateManager()))
