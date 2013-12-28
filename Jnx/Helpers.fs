namespace Jnx.Helpers

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
