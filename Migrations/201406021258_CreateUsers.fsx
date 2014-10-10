#load "../Tasks/MigrationBase.fsx"

open Fake
open MigrationBase

Target "Upgrade" (fun _ ->
    exec @"CREATE TABLE coins_user (
            identifier UUID NOT NULL,
            email VARCHAR(150) NOT NULL,
            name VARCHAR(150),
            provider_name VARCHAR(50) NOT NULL,
            provider_identity VARCHAR(150) NOT NULL,
            provider_picture VARCHAR(255) NOT NULL,
            approved BOOLEAN NOT NULL,
            roles INTEGER NOT NULL,
            CONSTRAINT pk_user PRIMARY KEY (identifier),
            CONSTRAINT ak_user_provider_name_provider_identity UNIQUE (provider_name, provider_identity),
            CONSTRAINT ak_user_email UNIQUE (email))"
)

Target "Downgrade" (fun _ -> exec @"DROP TABLE coins_user")

RunTargetOrDefault "Upgrade"
