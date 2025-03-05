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
    
let private compile _ = exec { run "dotnet" "build" "-c" "release" }

let private build _ = printfn "build"
let private release _ = printfn "release"

let private version _ =
    let version = Software.Version
    printfn $"Informational version: %s{version.AsString}"
    printfn $"Semantic version: %s{version.NormalizeToShorter()}"
    let otelVersion = Software.OpenTelemetryVersion;
    printfn $"OpenTelemetry version: %s{otelVersion.AsString}"
    let otelAutoVersion = Software.OpenTelemetryAutoInstrumentationVersion;
    printfn $"OpenTelemetry Auto Instrumentation version: %s{otelAutoVersion.AsString}"
    
let private generatePackages _ = exec { run "dotnet" "pack" }

let private format _ = exec { run "dotnet" "format" "--verbosity" "quiet" }

let private checkFormat _ =
    match exec { exit_code_of "dotnet" "format" "--verify-no-changes" } with
    | 0 -> printfn "There are no dotnet formatting violations, continuing the build."
    | _ -> failwithf "There are dotnet formatting violations. Call `dotnet format` to fix or specify -c to ./build.sh to skip this check"

let private pristineCheck (arguments:ParseResults<Build>) =
    let skipCheck = arguments.TryGetResult Skip_Dirty_Check |> Option.isSome
    match skipCheck, Information.isCleanWorkingCopy "." with
    | true, _ -> printfn "Skip checking for clean working copy since -c is specified"
    | _, true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> failwithf "The checkout folder has pending changes, aborting. Specify -c to ./build.sh to skip this check"
    
    match skipCheck, (exec { exit_code_of "dotnet" "format" "--verify-no-changes" }) with
    | true, _ -> printfn "Skip formatting checks since -c is specified"
    | _, 0  -> printfn "There are no dotnet formatting violations, continuing the build."
    | _ -> failwithf "There are dotnet formatting violations. Call `dotnet format` to fix or specify -c to ./build.sh to skip this check"

let private runTests suite _ =
    let logger =
        // use junit xml logging locally, github actions logs using console out formats
        match BuildServer.isGitHubActionsBuild with
        | true -> "--logger:\"GitHubActions;summary.includePassedTests=false;summary.includeNotFoundTests=false\""
        | false ->
            let testOutputPath = Paths.ArtifactPath "tests"
            let junitOutput = Path.Combine(testOutputPath.FullName, "junit-{assembly}-{framework}-test-results.xml")
            let loggerPathArgs = $"LogFilePath=%s{junitOutput}"
            $"--logger:\"junit;%s{loggerPathArgs}\""
    let filterArgs =
        match suite with
        | All -> []
        | Skip_All -> ["--filter"; "FullyQualifiedName~.SKIPPING.ALL.TESTS"]
        | Unit ->  [ "--filter"; "FullyQualifiedName~.Tests" ]
        | Integration -> [ "--filter"; "FullyQualifiedName~.IntegrationTests" ]
        | E2E -> [ "--filter"; "FullyQualifiedName~.EndToEndTests" ]
        | Skip_E2E -> [ "--filter"; "FullyQualifiedName!~.EndToEndTests" ]

    let settingsArg = ["-s"; "tests/.runsettings"]
    let tfmArgs = 
      if OS.Current = Windows then [] 
      elif suite.Equals(E2E) then ["-f"; "net8.0"]
      else ["-f"; "net9.0"]
    exec {
        env (Map ["TEST_SUITE", suite.SuitName])
        run "dotnet" (
            ["test"; "-c"; "release"; "--no-restore"; "--no-build"; logger]
            @ settingsArg
            @ filterArgs
            @ tfmArgs
            @ ["--"; "RunConfiguration.CollectSourceInformation=true"]
        )
    }

let private test (arguments:ParseResults<Build>) =
    let arg = arguments.TryGetResult Test_Suite
    match arg with
    | None -> runTests TestSuite.All arguments 
    | Some suite ->
        match suite with
        | Skip_All -> printfn "Skipping tests because --test-suite skip was provided"
        | _ -> runTests suite arguments   

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
        let tfm = "net8.0" // We use net8.0 as AutoInstrumentation doesn't have a net9.0 target
        let args =
            [
                "assembly-differ"
                $"previous-nuget|%s{p}|%s{currentVersion}|%s{tfm}";
                $"directory|.artifacts/bin/%s{p}/release_%s{tfm}";
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
        | Compile -> Build.Step compile
        | Build -> Build.Cmd [Clean; CheckFormat; Compile] [] build
        
        | End_To_End -> Build.Cmd [] [Build] <| runTests E2E
        | Integrate -> Build.Cmd [] [Build] <| runTests Integration
        | Unit_Test -> Build.Cmd [] [Build] <| runTests Unit
        | Test -> Build.Cmd [] [Build] test
        
        | Redistribute -> Build.Cmd [] [] Packaging.redistribute
        
        | Release -> 
            Build.Cmd 
                [PristineCheck; Build; Redistribute]
                [ValidateLicenses; GeneratePackages; ValidatePackages; GenerateReleaseNotes; GenerateApiChanges]
                release

        | Format -> Build.Step format

        // steps
        | CheckFormat -> Build.Step checkFormat
        | PristineCheck -> Build.Step pristineCheck
        | GeneratePackages -> Build.Step generatePackages
        | ValidateLicenses -> Build.Step validateLicenses
        | ValidatePackages -> Build.Step validatePackages
        | GenerateReleaseNotes -> Build.Step generateReleaseNotes
        | GenerateApiChanges -> Build.Step generateApiChanges
            
        // flags
        | Single_Target
        | Test_Suite _
        | Token _
        | Skip_Dirty_Check -> Build.Ignore

    for target in Build.Targets do
        let setup = wireCommandLine target 
        setup target parsed