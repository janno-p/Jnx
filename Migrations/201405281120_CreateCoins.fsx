#load "../Tasks/MigrationBase.fsx"

open Fake
open Jnx.Tasks.Migrations
open MigrationBase

type coins_country = { code : string }
type coins_coinage = { id : int }

type coins_coin = { image_uri : string
                    num_extra : int16
                    collected_at : System.DateTime option
                    collected_by : string option
                    id : int }

type coins_common_coin = { nominal_value : decimal
                           coinage_id : int
                           coin_id : int }

type coins_commemorative_coin = { year : int16
                                  common_issue : bool
                                  country_code : string 
                                  coin_id : int }

Target "Upgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_coin>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.image_uri @> (String 150)
            t.AddColumn <@ fun x -> x.num_extra @> SmallInt
            t.AddColumn <@ fun x -> x.collected_at @> Timestamp
            t.AddColumn <@ fun x -> x.collected_by @> (String 30)
        )
    )

    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_common_coin>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.nominal_value @> (Decimal (3, 2))
            t.AddColumn <@ fun x -> x.coinage_id @> Integer
            t.AddColumn <@ fun x -> x.coin_id @> Integer
            t.PrimaryKey <@ fun x -> x.coin_id @>
            t.ForeignKey <@ fun x (fk : coins_coin) -> (x.coin_id, fk.id) @>
            t.ForeignKey <@ fun x (fk : coins_coinage) -> (x.coinage_id, fk.id) @>
            t.Unique <@ fun x -> (x.nominal_value, x.coinage_id) @>
        )
    )

    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_commemorative_coin>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.year @> SmallInt
            t.AddColumn <@ fun x -> x.common_issue @> Boolean
            t.AddColumn <@ fun x -> x.country_code @> (String 2)
            t.AddColumn <@ fun x -> x.coin_id @> Integer
            t.PrimaryKey <@ fun x -> x.coin_id @>
            t.ForeignKey <@ fun x (fk : coins_coin) -> (x.coin_id, fk.id) @>
            t.ForeignKey <@ fun x (fk : coins_country) -> (x.country_code, fk.code) @>
        )
    )
)

Target "Downgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_commemorative_coin>) -> m.DropTable ())
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_common_coin>) -> m.DropTable ())
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<coins_coin>) -> m.DropTable ())
)

RunTargetOrDefault "Upgrade"
