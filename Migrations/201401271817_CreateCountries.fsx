#load "MigrationExpressions.fsx"

open MigrationExpressions

migration {
    createTable "coins_country"
    addColumn "id" 1
    addColumn "code" "et"
    addColumn "name" "Eesti"
    addColumn "genitive" "Eesti"
    addColumn "optional" (Some 0)
    primaryKey "pk_coins_country" ["id"]
} |> Migration.Execute
