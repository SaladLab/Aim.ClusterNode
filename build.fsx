#I @"packages/FAKE/tools"
#I @"packages/FAKE.BuildLib/lib/net451"
#r "FakeLib.dll"
#r "BuildLib.dll"

open Fake
open BuildLib

let solution = 
    initSolution
        "./Aim.ClusterNode.sln" "Release" 
        [ { emptyProject with Name = "Aim.ClusterNode" 
                              Folder = "./core/Aim.ClusterNode";
                              Dependencies=[("Akka", "")
                                            ("Akka.Interfaced", "")
                                            ("Akka.Cluster", "")
                                            ("Akka.Cluster.Utility", "")
                                            ("Common.Logging", "") ] } ]

Target "Clean" <| fun _ -> cleanBin

Target "AssemblyInfo" <| fun _ -> generateAssemblyInfo solution

Target "Restore" <| fun _ -> restoreNugetPackages solution

Target "Build" <| fun _ -> buildSolution solution

Target "Test" <| fun _ -> testSolution solution

Target "Cover" <| fun _ ->
    coverSolutionWithParams 
        (fun p -> { p with Filter = "+[Aim.ClusterNode*]* -[*.Tests]*" })
        solution

Target "Coverity" <| fun _ -> coveritySolution solution "SaladLab/Aim.ClusterNode"

Target "PackNuget" <| fun _ -> createNugetPackages solution

Target "Pack" <| fun _ -> ()

Target "PublishNuget" <| fun _ -> publishNugetPackages solution

Target "Publish" <| fun _ -> ()

Target "CI" <| fun _ -> ()

Target "Help" <| fun _ -> 
    showUsage solution (fun _ -> None)

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Test"

"Build" ==> "Cover"
"Restore" ==> "Coverity"

let isPublishOnly = getBuildParam "publishonly"

"Build" ==> "PackNuget" =?> ("PublishNuget", isPublishOnly = "")
"PackNuget" ==> "Pack"
"PublishNuget" ==> "Publish"

"Test" ==> "CI"
"Cover" ==> "CI"
"Publish" ==> "CI"

RunTargetOrDefault "Help"
