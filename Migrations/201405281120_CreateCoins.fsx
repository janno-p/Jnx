#load "../Tasks/MigrationBase.fsx"

open Fake
open MigrationBase

Target "Upgrade" (fun _ ->
    exec @"CREATE TABLE coins_coin (
            id SERIAL NOT NULL,
            image_uri VARCHAR(150) NOT NULL,
            num_extra SMALLINT NOT NULL,
            collected_at TIMESTAMP,
            collected_by VARCHAR(30),
            CONSTRAINT pk_coins_coin PRIMARY KEY (id))"

    exec @"CREATE TABLE coins_common_coin (
            coin_id INTEGER NOT NULL,
            nominal_value NUMERIC(3,2) NOT NULL,
            coinage_id INTEGER NOT NULL,
            CONSTRAINT pk_coins_common_coin PRIMARY KEY (coin_id),
            CONSTRAINT fk_coins_common_coin_coins_coin FOREIGN KEY (coin_id)
                REFERENCES coins_coin (id)
                ON UPDATE CASCADE ON DELETE CASCADE,
            CONSTRAINT fk_coins_common_coin_coins_coinage FOREIGN KEY (coinage_id)
                REFERENCES coins_coinage (id),
            CONSTRAINT ak_coins_common_coin_nominal_value_coinage_id UNIQUE (nominal_value, coinage_id))"

    exec @"CREATE TABLE coins_commemorative_coin (
            coin_id INTEGER NOT NULL,
            year SMALLINT NOT NULL,
            common_issue BOOLEAN NOT NULL,
            country_code VARCHAR(2) NOT NULL,
            CONSTRAINT pk_coins_commemorative_coin PRIMARY KEY (coin_id),
            CONSTRAINT fk_coins_commemorative_coin_coins_coin FOREIGN KEY (coin_id)
                REFERENCES coins_coin(id)
                ON UPDATE CASCADE ON DELETE CASCADE,
            CONSTRAINT fk_coins_commemorative_coin_coins_country FOREIGN KEY (country_code)
                REFERENCES coins_country (code))"
)

Target "Downgrade" (fun _ ->
    exec @"DROP TABLE coins_commemorative_coin"
    exec @"DROP TABLE coins_common_coin"
    exec @"DROP TABLE coins_coin"
)

RunTargetOrDefault "Upgrade"
