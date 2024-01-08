// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module Targets

open Argu
open System.IO
open CommandLine
open Fake.Core
open Fake.IO
open Fake.Tools.Git
open Proc.Fs
open BuildInformation

let private clean _ =
    exec { run "dotnet" "clean" } 
    Shell.cleanDir Paths.ArtifactFolder.FullName
    
let private build _ = exec { run "dotnet" "build" "-c" "Release" }

let private generatePackages _ = exec { run "dotnet" "pack" }
    
let private pristineCheck (arguments:ParseResults<Build>) =
    let doCheck = arguments.TryGetResult CleanCheckout |> Option.isSome
    match doCheck, Information.isCleanWorkingCopy "." with
    | _, true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | false, _ -> printf "Checkout is dirty but -c was specified to ignore this"
    | _ -> failwithf "The checkout folder has pending changes, aborting"

let rec private test _ =
    let testOutputPath = Paths.ArtifactPath "tests"
    let junitOutput = Path.Combine(testOutputPath.FullName, "junit-{assembly}-{framework}-test-results.xml")
    let loggerPathArgs = sprintf "LogFilePath=%s" junitOutput
    let loggerArg = sprintf "--logger:\"junit;%s\"" loggerPathArgs
    let tfmArgs =
        if OS.Current = OS.Windows then [] else ["-f"; "net8.0"]
    exec {
        run "dotnet" (["test"; "-c"; "Release"; loggerArg] @ tfmArgs)
    } 

let private validatePackages (arguments:ParseResults<Build>) =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RelativePathToRoot <| packagesPath.FullName
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(_.CreationTimeUtc)
        |> Seq.map (fun p -> Paths.RelativePathToRoot p.FullName)
        
    let args = ["-v"; Software.Version.AsString; "-k"; Software.SignKey; "-t"; output]
    nugetPackages
    |> Seq.iter (fun p ->
        exec { run "dotnet" (["nupkg-validator"; p] @ args) } 
    )

let private generateApiChanges (arguments:ParseResults<Build>) =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RelativePathToRoot <| packagesPath.FullName
    let currentVersion = Software.Version.NormalizeToShorter()
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(_.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RelativePathToRoot p.FullName).Replace("." + currentVersion, ""))
    nugetPackages
    |> Seq.iter(fun p ->
        let outputFile =
            let f = sprintf "breaking-changes-%s.md" p
            Path.Combine(output, f)
        let tfm = "net8.0"
        let args =
            [
                "assembly-differ"
                (sprintf "previous-nuget|%s|%s|%s" p currentVersion tfm);
                (sprintf "directory|src/%s/bin/Release/%s" p tfm);
                "-a"; "true"; "--target"; p; "-f"; "github-comment"; "--output"; outputFile
            ]
        exec { run "dotnet" args }
    )
    
let private generateReleaseNotes (arguments:ParseResults<Build>) =
    let currentVersion = Software.Version.NormalizeToShorter()
    let releaseNotesPath = Paths.ArtifactPath "release-notes"
    let output =
        Paths.RelativePathToRoot <| Path.Combine(releaseNotesPath.FullName, sprintf "release-notes-%s.md" currentVersion)
    let tokenArgs =
        match arguments.TryGetResult Token with
        | None -> []
        | Some token -> ["--token"; token;]
    let releaseNotesArgs =
        (Software.GithubMoniker.Split("/") |> Seq.toList)
        @ ["--version"; currentVersion
           "--label"; "enhancement"; "New Features"
           "--label"; "bug"; "Bug Fixes"
           "--label"; "documentation"; "Docs Improvements"
        ] @ tokenArgs
        @ ["--output"; output]
        
    let args = ["release-notes"] @ releaseNotesArgs
    exec { run "dotnet" args }

let private release _ = printfn "release"
    
let private publish _ = printfn "publish"

let private version _ =
    printfn $"hello world"
    let version = Software.Version
    printfn $"Informational version: %s{version.AsString}"
    printfn $"Semantic version: %s{version.NormalizeToShorter()}"

let Setup (parsed:ParseResults<Build>) =
    let wireSteps (t: Build) =
        match t with
        // commands
        | Clean -> Build.Step clean 
        | Version -> Build.Step version
        | Build -> Build.Cmd [Clean] [] build
        | Test -> Build.Cmd [Build] [] test
        | Release -> 
            Build.Cmd 
                [PristineCheck; Test]
                [GeneratePackages; ValidatePackages; GenerateReleaseNotes; GenerateApiChanges]
                release
            
        // steps
        | PristineCheck -> Build.Step pristineCheck  
        | GeneratePackages -> Build.Step generatePackages
        | ValidatePackages -> Build.Step validatePackages
        | GenerateReleaseNotes -> Build.Step generateReleaseNotes
        | GenerateApiChanges -> Build.Step generateApiChanges
            
        // flags
        | SingleTarget
        | Token _
        | CleanCheckout -> Build.Ignore

    for target in Build.Targets do
        let setup = wireSteps target 
        setup target parsed