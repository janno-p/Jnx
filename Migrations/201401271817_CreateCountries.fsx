#load "../Tasks/MigrationBase.fsx"

open Fake
open Jnx.Tasks.Migrations
open MigrationBase

type coins_country = { code : string
                       name : string
                       genitive : string }

Target "Upgrade" (fun _ ->
    let country code name genitive =
        { code = code; name = name; genitive = genitive }

    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_country>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.code @> (String 2)
            t.AddColumn <@ fun x -> x.name @> (String 50)
            t.AddColumn <@ fun x -> x.genitive @> (String 50)
            t.PrimaryKey <@ fun x -> x.code @>
            t.Unique <@ fun x -> x.code @>
            t.Unique <@ fun x -> x.name @>
        )

        m.Populate [ country "at" "Austria" "Austria"
                     country "be" "Belgia" "Belgia"
                     country "cy" "Küpros" "Küprose"
                     country "de" "Saksamaa" "Saksamaa"
                     country "es" "Hispaania" "Hispaania"
                     country "et" "Eesti" "Eesti"
                     country "fi" "Soome" "Soome"
                     country "fr" "Prantsusmaa" "Prantsusmaa"
                     country "gr" "Kreeka" "Kreeka"
                     country "ie" "Iirimaa" "Iirimaa"
                     country "it" "Itaalia" "Itaalia"
                     country "lu" "Luksemburg" "Luksemburgi"
                     country "lv" "Läti" "Läti"
                     country "mo" "Monaco" "Monaco"
                     country "mt" "Malta" "Malta"
                     country "nl" "Holland" "Hollandi"
                     country "pt" "Portugal" "Portugali"
                     country "sk" "Slovakkia" "Slovakkia"
                     country "sl" "Sloveenia" "Sloveenia"
                     country "sm" "San Marino" "San Marino"
                     country "va" "Vatikan" "Vatikani" ]
    )
)

Target "Downgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_country>) -> m.DropTable ())
)

RunTargetOrDefault "Upgrade"
