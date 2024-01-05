// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module Targets

open Argu
open System
open System.IO
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet
open System

type OS =
    | OSX
    | Windows
    | Linux

let getOS = 
    match int Environment.OSVersion.Platform with
    | 4 | 128 -> Linux
    | 6       -> OSX
    | _       -> Windows
    
let execWithTimeout binary args timeout =
    let opts =
        ExecArguments(binary, args |> List.map (sprintf "\"%s\"") |> List.toArray)
    let options = args |> String.concat " "
    printfn ":: Running command: %s %s" binary options
    let r = Proc.Exec(opts, timeout)

    match r.HasValue with
    | true -> r.Value
    | false -> failwithf "invocation of `%s` timed out" binary

let exec binary args =
    execWithTimeout binary args (TimeSpan.FromMinutes 10)
    
let private restoreTools = lazy(exec "dotnet" ["tool"; "restore"])
let private currentVersion =
    lazy(
        restoreTools.Value |> ignore
        let r = Proc.Start("dotnet", "minver", "--default-pre-release-phase", "canary", "-m", "0.1")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    )
let private currentVersionInformational =
    lazy(
        match Paths.IncludeGitHashInInformational with
        | false -> currentVersion.Value
        | true -> sprintf "%s+%s" currentVersion.Value (Information.getCurrentSHA1( "."))
    )

let private clean (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["clean"] |> ignore
    
let private build (arguments:ParseResults<Arguments>) = exec "dotnet" ["build"; "-c"; "Release"] |> ignore

let private pristineCheck (arguments:ParseResults<Arguments>) =
    let doCheck = arguments.TryGetResult CleanCheckout |> Option.defaultValue true
    match doCheck, Information.isCleanWorkingCopy "." with
    | _, true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | false, _ -> printf "Checkout is dirty but -c was specified to ignore this"
    | _ -> failwithf "The checkout folder has pending changes, aborting"

let rec private test (arguments:ParseResults<Arguments>) =
    let testOutputPath = Paths.ArtifactPath "tests"
    let junitOutput = Path.Combine(testOutputPath.FullName, "junit-{assembly}-{framework}-test-results.xml")
    let loggerPathArgs = sprintf "LogFilePath=%s" junitOutput
    let loggerArg = sprintf "--logger:\"junit;%s\"" loggerPathArgs
    let tfmArgs =
        if getOS = OS.Windows then [] else ["-f"; "net8.0"]
    exec "dotnet" (["test"; "-c"; "Release"; loggerArg] @ tfmArgs) |> ignore

let private generatePackages (arguments:ParseResults<Arguments>) = exec "dotnet" ["pack" ] |> ignore
    
let private validatePackages (arguments:ParseResults<Arguments>) =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RootRelative <| packagesPath.FullName
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(_.CreationTimeUtc)
        |> Seq.map (fun p -> Paths.RootRelative p.FullName)
        
    let jenkinsOnWindowsArgs =
        if Fake.Core.Environment.hasEnvironVar "JENKINS_URL" && Fake.Core.Environment.isWindows then ["-r"; "true"] else []
    
    let args = ["-v"; currentVersionInformational.Value; "-k"; Paths.SignKey; "-t"; output] @ jenkinsOnWindowsArgs
    nugetPackages |> Seq.iter (fun p -> exec "dotnet" (["nupkg-validator"; p] @ args) |> ignore)

let private generateApiChanges (arguments:ParseResults<Arguments>) =
    let packagesPath = Paths.ArtifactPath "package"
    let output = Paths.RootRelative <| packagesPath.FullName
    let currentVersion = currentVersion.Value
    let nugetPackages =
        packagesPath.GetFiles("*.nupkg", SearchOption.AllDirectories)
        |> Seq.sortByDescending(_.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RootRelative p.FullName).Replace("." + currentVersion, ""))
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
        
        exec "dotnet" args |> ignore
    )
    
let private generateReleaseNotes (arguments:ParseResults<Arguments>) =
    let currentVersion = currentVersion.Value
    let releaseNotesPath = Paths.ArtifactPath "release-notes"
    let output =
        Paths.RootRelative <| Path.Combine(releaseNotesPath.FullName, sprintf "release-notes-%s.md" currentVersion)
    let tokenArgs =
        match arguments.TryGetResult Token with
        | None -> []
        | Some token -> ["--token"; token;]
    let releaseNotesArgs =
        (Paths.Repository.Split("/") |> Seq.toList)
        @ ["--version"; currentVersion
           "--label"; "enhancement"; "New Features"
           "--label"; "bug"; "Bug Fixes"
           "--label"; "documentation"; "Docs Improvements"
        ] @ tokenArgs
        @ ["--output"; output]
        
    exec "dotnet" (["release-notes"] @ releaseNotesArgs) |> ignore

let private release (arguments:ParseResults<Arguments>) = printfn "release"
    
let private publish (arguments:ParseResults<Arguments>) = printfn "publish" 

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let step (name:string) action = Targets.Target(name, new Action(fun _ -> action(parsed)))
    
    let cmd (name:string) commandsBefore steps action =
        let singleTarget = (parsed.TryGetResult SingleTarget |> Option.defaultValue false)
        let deps =
            match (singleTarget, commandsBefore) with
            | (true, _) -> [] 
            | (_, Some d) -> d
            | _ -> []
        let steps = steps |> Option.defaultValue []
        Targets.Target(name, deps @ steps, Action(action))
        
    step Clean.Name clean
    cmd Build.Name None (Some [Clean.Name]) <| fun _ -> build parsed
    
    cmd Test.Name (Some [Build.Name;]) None <| fun _ -> test parsed
    
    step PristineCheck.Name pristineCheck
    step GeneratePackages.Name generatePackages 
    step ValidatePackages.Name validatePackages 
    step GenerateReleaseNotes.Name generateReleaseNotes
    step GenerateApiChanges.Name generateApiChanges
    cmd Release.Name
        (Some [PristineCheck.Name; Test.Name;])
        (Some [GeneratePackages.Name; ValidatePackages.Name; GenerateReleaseNotes.Name; GenerateApiChanges.Name])
        <| fun _ -> release parsed
        