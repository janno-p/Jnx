[<FunScript.JS>]
module Site

open FunScript
open FunScript.TypeScript
open Microsoft.FSharp.Quotations
open System.IO
open System.Reflection

let jq (selector : string) = Globals.Dollar.Invoke selector
let (?) jq name = jq ("#" + name)

let main () =
    let button = jq?``delete-country``
    button.click (fun _ ->
        if Globals.confirm "Oled kindel, et tahad antud euroala riiki kustutada?" then
            let settings = createEmpty<JQueryAjaxSettings>()
            settings._async <- false
            settings.``success <-`` (fun data _ _ -> Globals.window.location.href = "/countries" |> ignore; null)
            settings._type <- "DELETE"
            Globals.Dollar.ajax(button.attr("href"), settings) |> ignore
        null
    )

let compile () =
    let rootPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Scripts")

    let source = Compiler.Compiler.Compile(<@ main () @>)
    let wrapped = sprintf "$(document).ready(function () {\n%s\n});" source

    let fileName = Path.Combine(rootPath, "page.js")

    File.Delete fileName
    File.WriteAllText(fileName, wrapped)

(*
$(document).ready(function() {
    $('#delete-country').click(function() {
        var $a = $(this);
        if (confirm('Oled kindel, et tahad antud euroala riiki kustutada?')) {
            $.ajax({ url: $a.attr('href'),
                     async: false,
                     method: 'delete',
                     success: function (data) {
                        console.log(data);
                        window.location.href = '/countries';
                     }});
        }
        return false;
    });
});
*)