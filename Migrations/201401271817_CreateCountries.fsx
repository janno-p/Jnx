#load "MigrationExpressions.fsx"

open MigrationExpressions

migration {
    createTable "coins_country"
    addColumn "id" 1
    primaryKey "pk_coins_country" ["id"]
    addColumn "code" "et"
    unique "uq_coins_code" ["code"]
    addColumn "name" "Eesti"
    unique "uq_coins_name" ["name"]
    addColumn "genitive" "Eesti"
} |> Migration.Execute
