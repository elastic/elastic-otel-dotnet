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
    exec { run "dotnet" "clean" "-c" "release" }
    let removeArtifacts folder = Shell.cleanDir (Paths.ArtifactPath folder).FullName
    removeArtifacts "package"
    removeArtifacts "release-notes"
    removeArtifacts "tests"
    
let private build _ = exec { run "dotnet" "build" "-c" "release" }

let private release _ = printfn "release"
    
let private publish _ = printfn "publish"

let private version _ =
    let version = Software.Version
    printfn $"Informational version: %s{version.AsString}"
    printfn $"Semantic version: %s{version.NormalizeToShorter()}"
    
let private generatePackages _ = exec { run "dotnet" "pack" }
    
let private pristineCheck (arguments:ParseResults<Build>) =
    let skipCheck = arguments.TryGetResult SkipDirtyCheck |> Option.isSome
    match skipCheck, Information.isCleanWorkingCopy "." with
    | true, _ -> printfn "Checkout is dirty but -c was specified to ignore this"
    | _, true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> failwithf "The checkout folder has pending changes, aborting. Specify -c to ./build.sh to skip this check"

type TestSuite = | Unit | Integration | E2E | All

let private runTests suite _ =
    let logger =
        // use junit xml logging locally, github actions logs using console out formats
        match BuildServer.isGitHubActionsBuild with
        | true -> "--logger:\"GitHubActions;summary.includePassedTests=false\""
        | false ->
            let testOutputPath = Paths.ArtifactPath "tests"
            let junitOutput = Path.Combine(testOutputPath.FullName, "junit-{assembly}-{framework}-test-results.xml")
            let loggerPathArgs = $"LogFilePath=%s{junitOutput}"
            $"--logger:\"junit;%s{loggerPathArgs}\""
    let filterArgs =
        match suite with
        | All -> []
        | TestSuite.Unit ->  [ "--filter"; "FullyQualifiedName~.Tests" ]
        | TestSuite.Integration -> [ "--filter"; "FullyQualifiedName~.IntegrationTests" ]
        | TestSuite.E2E -> [ "--filter"; "FullyQualifiedName~.EndToEndTests" ]
        
    
    let settingsArg = ["-s"; "tests/.runsettings"]
    let tfmArgs = if OS.Current = OS.Windows then [] else ["-f"; "net8.0"]
    exec {
        run "dotnet" (
            ["test"; "-c"; "release"; "--no-restore"; "--no-build"; logger]
            @ settingsArg
            @ filterArgs
            @ tfmArgs
            @ ["--"; "RunConfiguration.CollectSourceInformation=true"]
        )
    }
    
let private test suite (arguments:ParseResults<Build>) =
    match arguments.TryGetResult SkipTests with
    | None -> runTests suite arguments 
    | Some _ -> printfn "Skipping tests because --skiptests was provided"

let private validateLicenses _ =
    let args = ["-u"; "-t"; "-i"; "Elastic.OpenTelemetry.sln"; "--use-project-assets-json"
                "--forbidden-license-types"; "build/forbidden-license-types.json"
                "--packages-filter"; "#System\..*#";]
    exec { run "dotnet" (["dotnet-project-licenses"] @ args) }

let private validatePackages _ =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RelativePathToRoot <| packagesPath.FullName
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Paths.RelativePathToRoot p.FullName)
        
    let args = ["-v"; Software.Version.AsString; "-k"; Software.SignKey; "-t"; output]
    nugetPackages
    |> Seq.iter (fun p ->
        exec { run "dotnet" (["nupkg-validator"; p] @ args) } 
    )

let private generateApiChanges _ =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RelativePathToRoot <| packagesPath.FullName
    let currentVersion = Software.Version.NormalizeToShorter()
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RelativePathToRoot p.FullName).Replace("." + currentVersion, ""))
    nugetPackages
    |> Seq.iter(fun p ->
        let outputFile = Path.Combine(output, $"breaking-changes-%s{p}.md")
        let tfm = "net8.0"
        let args =
            [
                "assembly-differ"
                $"previous-nuget|%s{p}|%s{currentVersion}|%s{tfm}";
                //$"directory|.artifacts/bin/%s{p}/release/%s{tfm}";
                $"directory|.artifacts/bin/%s{p}/release";
                "-a"; "true"; "--target"; p; "-f"; "github-comment"; "--output"; outputFile
            ]
        exec { run "dotnet" args }
    )
    
let private generateReleaseNotes (arguments:ParseResults<Build>) =
    let currentVersion = Software.Version.NormalizeToShorter()
    let releaseNotesPath = Paths.ArtifactPath "release-notes"
    let output =
        Paths.RelativePathToRoot <| Path.Combine(releaseNotesPath.FullName, $"release-notes-%s{currentVersion}.md")
    let tokenArgs =
        match arguments.TryGetResult Token with
        | None -> []
        | Some token -> ["--token"; token;]
    let releaseNotesArgs =
        (Software.GithubMoniker.Split("/") |> Seq.toList)
        @ ["--version"; currentVersion
           "--label"; "enhancement"; "Features"
           "--label"; "bug"; "Fixes"
           "--label"; "documentation"; "Documentation"
        ] @ tokenArgs
        @ ["--output"; output]
        
    let args = ["release-notes"] @ releaseNotesArgs
    exec { run "dotnet" args }

let Setup (parsed:ParseResults<Build>) =
    let wireCommandLine (t: Build) =
        match t with
        // commands
        | Version -> Build.Step version
        | Clean -> Build.Cmd [Version] [] clean
        | Build -> Build.Cmd [Clean] [] build
        
        | UnitTest -> Build.Cmd [Build] [] <| test TestSuite.Unit
        | Integrate -> Build.Cmd [Build] [] <| test TestSuite.Integration
        | EndToEnd -> Build.Cmd [Build] [] <| test TestSuite.E2E
        | Test -> Build.Cmd [UnitTest; Integrate; EndToEnd] [] ignore
        
        | Release -> 
            Build.Cmd 
                [PristineCheck; Test]
                [ValidateLicenses; GeneratePackages; ValidatePackages; GenerateReleaseNotes; GenerateApiChanges]
                release
            
        // steps
        | PristineCheck -> Build.Step pristineCheck  
        | GeneratePackages -> Build.Step generatePackages
        | ValidateLicenses -> Build.Step validateLicenses
        | ValidatePackages -> Build.Step validatePackages
        | GenerateReleaseNotes -> Build.Step generateReleaseNotes
        | GenerateApiChanges -> Build.Step generateApiChanges
            
        // flags
        | SingleTarget
        | SkipTests
        | Token _
        | SkipDirtyCheck -> Build.Ignore

    for target in Build.Targets do
        let setup = wireCommandLine target 
        setup target parsed