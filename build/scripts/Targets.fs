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
    exec { run "dotnet" "clean" "-c" "release" "/p:SkipBuildTool=true" }
    let removeArtifacts folder = Shell.cleanDir (Paths.ArtifactPath folder).FullName
    removeArtifacts "package"
    removeArtifacts "tests"
    
let private compile _ = exec { run "dotnet" "build" "-c" "release" "/p:SkipBuildTool=true" }

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

let private runTests suite (arguments:ParseResults<Build>) =
    let skipRestore = arguments.TryGetResult Skip_Restore |> Option.isSome
    let loggerArgs =
        match BuildServer.isGitHubActionsBuild with
        | true ->
            [ "--logger"; "GitHubActions;summary.includePassedTests=false;summary.includeNotFoundTests=false"
              "--logger"; "console;verbosity=detailed" ]
        | false -> []

    let filterArgs =
        match suite with
        | All -> []
        | Skip_All -> ["--filter"; "FullyQualifiedName~.SKIPPING.ALL.TESTS"]
        | Unit ->  [ "--filter"; "FullyQualifiedName~.Tests&FullyQualifiedName!~.BuildVerification.Tests&FullyQualifiedName!~.IntegrationTests&FullyQualifiedName!~.AotCompatibility.Tests&Category!=AotCompatibility" ]
        | Integration -> [ "--filter"; "FullyQualifiedName~.IntegrationTests" ]
        | AutoInstrumentation_Integration | OpenTelemetry_Integration -> []
        | Build_Verification -> []
        | Aot_Compatibility -> []

    // Build verification and individual integration suites target their own projects; all other suites run against the whole solution
    let projectArgs =
        match suite with
        | Build_Verification -> ["tests/Elastic.OpenTelemetry.BuildVerification.Tests/Elastic.OpenTelemetry.BuildVerification.Tests.csproj"]
        | Aot_Compatibility -> ["tests/Elastic.OpenTelemetry.AotCompatibility.Tests/Elastic.OpenTelemetry.AotCompatibility.Tests.csproj"]
        | AutoInstrumentation_Integration -> ["tests/AutoInstrumentation.IntegrationTests/AutoInstrumentation.IntegrationTests.csproj"]
        | OpenTelemetry_Integration -> ["tests/Elastic.OpenTelemetry.IntegrationTests/Elastic.OpenTelemetry.IntegrationTests.csproj"]
        | _ -> []

    let tfmArgs =
        if OS.Current = Windows then []
        else ["-f"; "net10.0"]
    let restoreArgs = if skipRestore then ["--no-restore"] else []

    exec {
        env (Map ["TEST_SUITE", suite.SuitName])
        run "dotnet" (
            ["test"; "-c"; "release"; "--no-build"]
            @ restoreArgs
            @ loggerArgs
            @ projectArgs
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
    let args = ["-t"; "-i"; "Elastic.OpenTelemetry.slnx";
                "--allowed-license-types";"build/allowed-licenses.json"; 
                "--exclude-projects-matching"; "build/exclude-license-check.json"; "--ignored-packages"; "build/ignored-packages-license-check.json"
                "-o"; "JsonPretty"]
    exec { run "dotnet" (["nuget-license"] @ args) }

let private validatePackages _ =
    if OS.Current = Windows then
        printfn "Skipping package validation on Windows"
    else
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
    
let Setup (parsed:ParseResults<Build>) =
    let skipBuild = parsed.TryGetResult Skip_Build |> Option.isSome
    let testComposedOf = if skipBuild then [] else [Build]

    let wireCommandLine (t: Build) =
        match t with
        // commands
        | Version -> Build.Step version
        | Clean -> Build.Cmd [Version] [] clean
        | Compile -> Build.Step compile
        | Build -> Build.Cmd [Clean; CheckFormat; Compile] [] build
        
        | Integrate -> Build.Cmd [Redistribute] testComposedOf <| runTests Integration
        | Integrate_AutoInstrumentation -> Build.Cmd [Redistribute] testComposedOf <| runTests AutoInstrumentation_Integration
        | Integrate_OpenTelemetry -> Build.Cmd [Redistribute] testComposedOf <| runTests OpenTelemetry_Integration
        | Unit_Test -> Build.Cmd [] testComposedOf <| runTests Unit
        | Build_Verify -> Build.Cmd [] testComposedOf <| runTests Build_Verification
        | Aot_Compat -> Build.Cmd [] testComposedOf <| runTests Aot_Compatibility
        | Test -> Build.Cmd [] testComposedOf test
        
        | Redistribute -> Build.Cmd [Compile] [] Packaging.redistribute
        
        | Release -> 
            Build.Cmd 
                [PristineCheck; Build; Redistribute]
                [ValidateLicenses; GeneratePackages; ValidatePackages]
                release

        | Format -> Build.Step format

        // steps
        | CheckFormat -> Build.Step checkFormat
        | PristineCheck -> Build.Step pristineCheck
        | GeneratePackages -> Build.Step generatePackages
        | ValidateLicenses -> Build.Step validateLicenses
        | ValidatePackages -> Build.Step validatePackages
            
        // flags
        | Single_Target
        | Test_Suite _
        | Token _
        | Skip_Dirty_Check
        | Skip_Build
        | Skip_Restore -> Build.Ignore

    for target in Build.Targets do
        let setup = wireCommandLine target 
        setup target parsed