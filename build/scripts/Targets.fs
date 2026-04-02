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

type private TestProject =
    {
        Path: string
        TfmArgs: string list
    }

let private unitTestProject =
    {
        Path = "tests/Elastic.OpenTelemetry.Tests/Elastic.OpenTelemetry.Tests.csproj"
        TfmArgs = if OS.Current = Windows then [] else ["-f"; "net10.0"]
    }

let private buildVerificationTestProject =
    {
        Path = "tests/Elastic.OpenTelemetry.BuildVerification.Tests/Elastic.OpenTelemetry.BuildVerification.Tests.csproj"
        TfmArgs = []
    }

let private aotCompatibilityTestProject =
    {
        Path = "tests/Elastic.OpenTelemetry.AotCompatibility.Tests/Elastic.OpenTelemetry.AotCompatibility.Tests.csproj"
        TfmArgs = []
    }

let private autoInstrumentationIntegrationTestProject =
    {
        Path = "tests/AutoInstrumentation.IntegrationTests/AutoInstrumentation.IntegrationTests.csproj"
        TfmArgs = []
    }

let private openTelemetryIntegrationTestProject =
    {
        Path = "tests/Elastic.OpenTelemetry.IntegrationTests/Elastic.OpenTelemetry.IntegrationTests.csproj"
        TfmArgs = []
    }

let private getTestProjects suite =
    match suite with
    | All ->
        [ unitTestProject
          buildVerificationTestProject
          aotCompatibilityTestProject
          autoInstrumentationIntegrationTestProject
          openTelemetryIntegrationTestProject ]
    | Unit -> [unitTestProject]
    | Integration ->
        [ autoInstrumentationIntegrationTestProject
          openTelemetryIntegrationTestProject ]
    | AutoInstrumentation_Integration -> [autoInstrumentationIntegrationTestProject]
    | OpenTelemetry_Integration -> [openTelemetryIntegrationTestProject]
    | Build_Verification -> [buildVerificationTestProject]
    | Aot_Compatibility -> [aotCompatibilityTestProject]
    | Skip_All -> []

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

let private runTests (suite: TestSuite) (arguments:ParseResults<Build>) =
    let skipRestore = arguments.TryGetResult Skip_Restore |> Option.isSome
    let skipBuild = arguments.TryGetResult Skip_Build |> Option.isSome
    let loggerArgs =
        match BuildServer.isGitHubActionsBuild with
        | true ->
            [ "--logger"; "GitHubActions;summary.includePassedTests=false;summary.includeNotFoundTests=false"
              "--logger"; "console;verbosity=detailed" ]
        | false -> []

    let runDotnetTest (project: TestProject) =
        let restoreArgs = if skipRestore then ["--no-restore"] else []
        let noBuildArgs = if skipBuild then ["--no-build"] else []

        exec {
            env (Map ["TEST_SUITE", suite.SuitName])
            run "dotnet" (
                ["test"; "-c"; "release"]
                @ noBuildArgs
                @ restoreArgs
                @ loggerArgs
                @ [project.Path]
                @ project.TfmArgs
                @ ["--"; "RunConfiguration.CollectSourceInformation=true"]
            )
        }

    let runTestProject project =
        try
            runDotnetTest project
            None
        with ex ->
            printfn $"Test project failed: %s{project.Path}"
            Some (project.Path, ex.Message)

    let failures =
        getTestProjects suite
        |> List.choose runTestProject

    if failures |> List.isEmpty |> not then
        let details =
            failures
            |> List.map (fun (projectPath, message) -> $"- %s{projectPath}: %s{message}")
            |> String.concat "\n"
        failwithf "One or more test projects failed:\n%s" details

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