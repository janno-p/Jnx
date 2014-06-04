#load "../Tasks/MigrationBase.fsx"

open Fake
open Jnx.Tasks.Migrations
open MigrationBase

type user = { identifier : System.Guid
              email : string
              name : string option
              provider_name : string
              provider_identity : string
              provider_picture : string
              approved : bool
              roles : int }

Target "Upgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<user>) ->
        m.CreateTable (fun t ->
            t.AddColumn <@ fun x -> x.identifier @> Uuid
            t.AddColumn <@ fun x -> x.email @> (String 150)
            t.AddColumn <@ fun x -> x.name @> (String 150)
            t.AddColumn <@ fun x -> x.provider_name @> (String 50)
            t.AddColumn <@ fun x -> x.provider_identity @> (String 150)
            t.AddColumn <@ fun x -> x.provider_picture @> (String 255)
            t.AddColumn <@ fun x -> x.approved @> Boolean
            t.AddColumn <@ fun x -> x.roles @> Integer
            t.PrimaryKey <@ fun x -> (x.provider_name, x.provider_identity) @>
        )
    )
)

Target "Downgrade" (fun _ ->
    Migrate (OpenConnection()) (fun (m : IMigrationBuilder<user>) -> m.DropTable ())
)

RunTargetOrDefault "Upgrade"
