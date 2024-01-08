// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module CommandLine

open Argu
open Microsoft.FSharp.Reflection
open System
open Bullseye

type Build =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Clean
    | [<CliPrefix(CliPrefix.None);SubCommand>] Version
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build
    | [<CliPrefix(CliPrefix.None);SubCommand>] Test
    
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PristineCheck 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GeneratePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidatePackages 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateReleaseNotes 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateApiChanges 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release
    
    | [<Inherit;AltCommandLine("-s")>] SingleTarget
    | [<Inherit>] Token of string 
    | [<Inherit;AltCommandLine("-c")>] CleanCheckout
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            // commands
            | Clean -> "clean known output locations"
            | Version -> "print version information"
            | Build -> "Run build"
            | Test -> "Runs build then tests"
            | Release -> "runs build, tests, and create and validates the packages shy of publishing them"
            
            // steps
            | PristineCheck  
            | GeneratePackages
            | ValidatePackages 
            | GenerateReleaseNotes
            | GenerateApiChanges -> "Undocumented, dependent target"
            
            // flags
            | SingleTarget -> "Runs the provided sub command without running their dependencies"
            | Token _ -> "Token to be used to authenticate with github"
            | CleanCheckout -> "Skip the clean checkout check that guards the release/publish targets"
            
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
        let singleTarget = parsed.TryGetResult SingleTarget |> Option.isSome
        let dependsOn = if singleTarget then [] else dependsOn
            
        let steps = dependsOn @ composedOf |> List.map (_.StepName)
        Targets.Target(target.StepName, steps, Action(fun _ -> action parsed))
    
