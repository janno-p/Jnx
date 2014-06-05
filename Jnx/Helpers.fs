namespace Jnx.Helpers

open Jnx.Repositories
open Nancy.ViewEngines.Razor
open System
open System.Text
open System.Text.RegularExpressions
open System.Runtime.CompilerServices

[<Extension>]
module HtmlHelperExtensions =
    let appendIf condition (str:string) (builder:StringBuilder) =
        builder.Append(match condition with | true -> str | _ -> "")

    let append (str:string) (builder:StringBuilder) =
        builder.Append(str)

    let toHtml (builder:StringBuilder) =
        new NonEncodedHtmlString(builder.ToString())

    [<Extension>]
    let MenuItem (this:HtmlHelpers<'T>) text path =
        let current = Regex.Replace(this.RenderContext.Context.Request.Path, @"/{2,}", "/")
        new StringBuilder()
        |> append "<li" |> appendIf (current.StartsWith(path)) @" class=""active""" |> append ">"
        |> append "<a" |> append (sprintf @" href=""%s""" path) |> append ">"
        |> append text
        |> append "</a>"
        |> append "</li>"
        |> toHtml

    [<Extension>]
    let ProgressType (this:HtmlHelpers<'T>) value =
        match value with
        | x when x < 50 -> "danger"
        | x when x < 100 -> "warning"
        | _ -> "success"

    [<Extension>]
    let PercentRatio (this : HtmlHelpers<'T>) current total =
        match total with
        | 0 -> 100
        | _ -> current * 100 / total

    [<Extension>]
    let CoinNominalValue (this : HtmlHelpers<'T>) (coin : Coin) =
        match coin.Type with
        | CommonCoin (_, nominalValue) -> nominalValue
        | _ -> 2M
        |> sprintf "&euro;%.2f"

    [<Extension>]
    let CoinCommemorativeYear (this : HtmlHelpers<'T>) (coin : Coin) =
        match coin.Type with
        | CommemorativeCoin (_, year, _) -> year.ToString()
        | _ -> ""

    [<Extension>]
    let IsCommonCoin (this : HtmlHelpers<'T>) (coin : Coin) =
        match coin.Type with
        | CommonCoin _ -> true
        | _ -> false

    [<Extension>]
    let FlashHasKey (this : HtmlHelpers<'T>) (key : string) =
        match this.RenderContext.Context.Items.TryGetValue Jnx.NancyExtensions.FlashStoreKey with
        | true, (:? Jnx.NancyExtensions.FlashStore as store) -> store.ContainsKey key
        | _ -> false

    [<Extension>]
    let FlashValue (this : HtmlHelpers<'T>) (key : string) =
        match this.RenderContext.Context.Items.TryGetValue Jnx.NancyExtensions.FlashStoreKey with
        | true, (:? Jnx.NancyExtensions.FlashStore as store) ->
            match store.TryGetValue key with
            | true, value -> value
            | _ -> null
        | _ -> null
