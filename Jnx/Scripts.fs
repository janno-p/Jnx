[<FunScript.JS>]
module Site

open FunScript
open FunScript.TypeScript
open Microsoft.FSharp.Quotations
open System.IO
open System.Reflection

let jq (selector : string) = Globals.Dollar.Invoke selector
let (?) jq name = jq ("#" + name)

[<JSEmit("$.fn.editable.defaults.mode = 'inline';")>]
let enableInlineEditable (): unit = failwith "never"

[<JSEmit("return {0}.editable()")>]
let makeEditable (x: JQuery): JQuery = failwith "never"

[<JSEmit("return swal({0}, {1})")>]
let initSwal (x: System.Collections.Generic.Dictionary<string, obj>) (f: 'a -> obj): unit = failwith "never"

[<JSEmit("return swal({0}, {1}, 'success')")>]
let swalSuccess (title: string) (text: string): obj = failwith "never"

[<JSEmitInlineAttribute("$(this)")>]
let this (): JQuery = failwith "never"

let main () =
    let button = (jq ".delete-country")
    button.click (fun _ ->
        let me = this()
        let name = me.parent().parent().find("td a").first().text()
        let swalSettings = System.Collections.Generic.Dictionary<_, obj>()
        swalSettings.Add("title", "Euroala riigi kustutamine!")
        swalSettings.Add("text", "Oled kindel, et tahad kustutada euroala riiki " + name + "?")
        swalSettings.Add("type", "warning")
        swalSettings.Add("showCancelButton", true)
        swalSettings.Add("cancelButtonText", "Ei")
        swalSettings.Add("confirmButtonColor", "#DD6B55")
        swalSettings.Add("confirmButtonText", "Jah, kustuta!")
        initSwal swalSettings (fun isConfirm ->
            if isConfirm then
                let settings = createEmpty<JQueryAjaxSettings>()
                settings._async <- false
                settings.``complete <-`` (fun xhr _ ->
                    Globals.window.location.href = "/countries" |> ignore
                    null
                )
                settings._type <- "DELETE"
                Globals.Dollar.ajax(me.attr("href"), settings) |> ignore
                swalSuccess "Kustutatud!" ("Euroalariik " + name + " on kustutatud")
            else null
            )
        box false
    ) |> ignore
    //enableInlineEditable()
    makeEditable(jq ".x-editable") |> ignore

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