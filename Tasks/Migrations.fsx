#I "../packages/FAKE.2.4.8.0/tools"

#r "FakeLib.dll"

open Fake

Target "Test" (fun _ ->
    trace "Testing stuff ..."
)

Target "Deploy" (fun _ ->
    trace "Heavy deploy action"
)

"Test" ==> "Deploy"

Run "Deploy"
