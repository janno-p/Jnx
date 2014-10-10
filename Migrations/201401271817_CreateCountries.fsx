#load "../Tasks/MigrationBase.fsx"

open Fake
open MigrationBase

Target "Upgrade" (fun _ ->
    exec @"CREATE TABLE coins_country (
            code VARCHAR(2) NOT NULL,
            name VARCHAR(50) NOT NULL,
            genitive VARCHAR(50) NOT NULL,
            CONSTRAINT pk_coins_country PRIMARY KEY (code),
            CONSTRAINT ak_coins_country_name UNIQUE (name))"

    exec @"INSERT INTO coins_country (code, name, genitive)
                VALUES ('at', 'Austria', 'Austria'),
                       ('be', 'Belgia', 'Belgia'),
                       ('cy', 'Küpros', 'Küprose'),
                       ('de', 'Saksamaa', 'Saksamaa'),
                       ('es', 'Hispaania', 'Hispaania'),
                       ('et', 'Eesti', 'Eesti'),
                       ('fi', 'Soome', 'Soome'),
                       ('fr', 'Prantsusmaa', 'Prantsusmaa'),
                       ('gr', 'Kreeka', 'Kreeka'),
                       ('ie', 'Iirimaa', 'Iirimaa'),
                       ('it', 'Itaalia', 'Itaalia'),
                       ('lt', 'Leedu', 'Leedu'),
                       ('lu', 'Luksemburg', 'Luksemburgi'),
                       ('lv', 'Läti', 'Läti'),
                       ('mo', 'Monaco', 'Monaco'),
                       ('mt', 'Malta', 'Malta'),
                       ('nl', 'Holland', 'Hollandi'),
                       ('pt', 'Portugal', 'Portugali'),
                       ('sk', 'Slovakkia', 'Slovakkia'),
                       ('sl', 'Sloveenia', 'Sloveenia'),
                       ('sm', 'San Marino', 'San Marino'),
                       ('va', 'Vatikan', 'Vatikani')"
)

Target "Downgrade" (fun _ -> exec @"DROP TABLE coins_country")

RunTargetOrDefault "Upgrade"
