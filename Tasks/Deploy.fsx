#I "../packages/FAKE.2.4.8.0/tools"

#r "FakeLib.dll"

open Fake

Target "Deploy" (fun _ ->
    trace "Deploying ..."
)

RunTargetOrDefault "Deploy"
