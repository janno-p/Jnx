#load "../Tasks/MigrationBase.fsx"

open Fake
open MigrationBase

Target "Upgrade" (fun _ ->
    exec @"CREATE TABLE coins_coinage (
            id SERIAL NOT NULL,
            year SMALLINT NOT NULL,
            name VARCHAR(150) NOT NULL,
            country_code VARCHAR(2) NOT NULL,
            CONSTRAINT pk_coins_coinage PRIMARY KEY (id),
            CONSTRAINT fk_coins_coinage_coins_country FOREIGN KEY (country_code)
                REFERENCES coins_country (code),
            CONSTRAINT ak_coins_coinage_name UNIQUE (name))"
)

Target "Downgrade" (fun _ -> exec @"DROP TABLE coins_coinage")

RunTargetOrDefault "Upgrade"
