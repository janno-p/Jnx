#load "../Tasks/MigrationBase.fsx"

open Fake
open Jnx.Tasks.Migrations
open MigrationBase

type coins_coinage = { year : int16
                       name : string
                       country_code : string }

type coins_country = { code : string }

Target "Upgrade" (fun _ ->
    let coinage year name country_code =
        { year = year; name = name; country_code = country_code }

    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_coinage>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.year @> SmallInt
            t.AddColumn <@ fun x -> x.name @> (String 150)
            t.AddColumn <@ fun x -> x.country_code @> (String 2)
            t.Unique <@ fun x -> x.name @>
            t.ForeignKey <@ fun x (fk : coins_country) -> (x.country_code, fk.code) @>
        )
    )
)

Target "Downgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_coinage>) -> m.DropTable ())
)

RunTargetOrDefault "Upgrade"
