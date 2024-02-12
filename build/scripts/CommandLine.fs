// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module CommandLine

open Argu
open Microsoft.FSharp.Reflection
open System
open Bullseye

type TestSuite = All | Unit | Integration | E2E | Skip_All | Skip_E2E 

type Build =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Clean
    | [<CliPrefix(CliPrefix.None);SubCommand>] Version
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build
    | [<CliPrefix(CliPrefix.None);SubCommand>] Test
    
    | [<CliPrefix(CliPrefix.None);SubCommand>] Unit_Test
    | [<CliPrefix(CliPrefix.None);SubCommand>] End_To_End
    
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PristineCheck 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GeneratePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidateLicenses 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidatePackages 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateReleaseNotes 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateApiChanges 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release
    
    | [<Inherit;AltCommandLine("-s")>] Single_Target
    | [<Inherit>] Token of string 
    | [<Inherit;AltCommandLine("-c")>] Skip_Dirty_Check
    | [<Inherit;>] Test_Suite of TestSuite 
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            // commands
            | Clean -> "clean known output locations"
            | Version -> "print version information"
            | Build -> "Run build"
            
            | Unit_Test -> "alias to providing: test --test-suite=unit"
            | End_To_End -> "alias to providing: test --test-suite=e2e"
            | Test -> "runs a clean build and then runs all the tests unless --test-suite is provided"
            | Release -> "runs build, tests, and create and validates the packages shy of publishing them"
            
            // steps
            | PristineCheck  
            | GeneratePackages
            | ValidateLicenses
            | ValidatePackages 
            | GenerateReleaseNotes
            | GenerateApiChanges -> "Undocumented, dependent target"
            
            // flags
            | Single_Target -> "Runs the provided sub command without running their dependencies"
            | Token _ -> "Token to be used to authenticate with github"
            | Skip_Dirty_Check -> "Skip the clean checkout check that guards the release/publish targets"
            | Test_Suite _ -> "Specify the test suite to run, defaults to all"

            
    member this.StepName =
        match FSharpValue.GetUnionFields(this, typeof<Build>) with
        | case, _ -> case.Name.ToLowerInvariant()
        
    static member Targets =
        let cases = FSharpType.GetUnionCases(typeof<Build>)
        seq {
             for c in cases do
                 if c.GetFields().Length = 0 then
                     FSharpValue.MakeUnion(c, [| |]) :?> Build
        }
        
    static member Ignore (_: Build) _ = ()
        
    static member Step action (target: Build) parsed =
        Targets.Target(target.StepName, Action(fun _ -> action(parsed)))

    static member Cmd (dependsOn: Build list) (composedOf: Build list) action (target: Build) (parsed: ParseResults<Build>) =
        let singleTarget = parsed.TryGetResult Single_Target |> Option.isSome
        let dependsOn = if singleTarget then [] else dependsOn
            
        let steps = dependsOn @ composedOf |> List.map (_.StepName)
        Targets.Target(target.StepName, steps, Action(fun _ -> action parsed))
    
