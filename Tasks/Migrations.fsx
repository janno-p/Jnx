#!/usr/bin/env fsharpi

#load "MigrationBase.fsx"

open Fake
open MigrationBase
open Npgsql
open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open System.Threading

let IsInitialized (connection : NpgsqlConnection) =
    use command = connection.CreateCommand()
    command.CommandText <- @"SELECT COUNT(*) FROM information_schema.tables WHERE table_catalog=:schemaname AND table_name='migrations'"
    command.Parameters.Add("schemaname", connection.Database) |> ignore
    unbox<int64> (command.ExecuteScalar()) > 0L

let GetLastMigration (connection : NpgsqlConnection) =
    use command = connection.CreateCommand()
    command.CommandText <- @"SELECT MAX(m.migration) FROM (SELECT migration FROM migrations UNION ALL SELECT '0' AS migration) m"
    unbox<string> (command.ExecuteScalar())

let SetLastMigration (connection : NpgsqlConnection) (migrationName : string) =
    use command = connection.CreateCommand()
    command.CommandText <- @"INSERT INTO migrations (migration) VALUES (:name)"
    command.Parameters.Add("name", migrationName) |> ignore
    command.ExecuteNonQuery() |> ignore

let private FakeStartInfo script workingDirectory target =
    (fun (info: ProcessStartInfo) ->
        info.FileName <- (__SOURCE_DIRECTORY__ @@ ".." @@ "packages" @@ "FAKE.2.4.8.0" @@ "tools" @@ "FAKE.exe")
        info.Arguments <- String.concat " " [script; target ]
        info.WorkingDirectory <- workingDirectory)

let private ExecuteFake workingDirectory script target =
    let (result, messages) =
        ExecProcessRedirected
            (FakeStartInfo script workingDirectory target)
            TimeSpan.MaxValue
    Thread.Sleep 1000
    (result, messages)

let private Init (connection : NpgsqlConnection) =
    if not (IsInitialized connection) then
        use command = connection.CreateCommand()
        command.CommandText <- @"CREATE TABLE ""migrations"" (
                                    ""migration"" VARCHAR(12) NOT NULL,
                                    CONSTRAINT ""pk_migrations"" PRIMARY KEY (""migration"")
                                 )"
        command.ExecuteNonQuery() |> ignore

Target "Migrate" (fun _ ->
    use connection = OpenConnection()
    Init connection
    let migrationDir = DirectoryInfo (__SOURCE_DIRECTORY__ @@ ".." @@ "Migrations")
    let files = migrationDir.GetFiles("????????????_*.fsx")
                |> Array.filter (fun fi -> Regex.IsMatch(fi.Name, @"^\d{12}_\w+\.fsx$"))
                |> Array.map (fun fi -> (fi.Name.Substring(0, 12), fi.Name))
                |> Array.sortBy (fun (id, _) -> id)
    if (files |> Array.length) <> (files |> Seq.distinct |> Seq.length) then
        failwith "Duplication migration files detected."
    let lastMigration = GetLastMigration connection
    let pendingFiles = match files |> Array.exists (fun (id, _) -> id = lastMigration) with
                       | true -> files |> Seq.skipWhile (fun (id, _) -> id <> lastMigration)
                                       |> Seq.skip 1
                                       |> Seq.toArray
                       | false -> files
    let appliedFiles = pendingFiles
                       |> Seq.takeWhile (fun (_, file) ->
                            let success, output = ExecuteFake migrationDir.FullName file "Upgrade"
                            output |> Seq.iter (fun x -> match x.IsError with | true -> traceError x.Message | _ -> trace x.Message)
                            success )
                       |> Seq.toArray
    if not (appliedFiles |> Array.isEmpty) then
        SetLastMigration connection (fst (appliedFiles |> Seq.last))
)

RunTargetOrDefault "Migrate"
