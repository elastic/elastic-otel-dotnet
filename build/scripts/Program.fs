// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module Program

open Argu
open Bullseye
open ProcNet
open CommandLine
    
[<EntryPoint>]
let main argv =
    let argv = if argv.Length = 0 then ["build"] |> Array.ofList else argv
    let parser = ArgumentParser.Create<Build>(programName = "./build.sh")
    let parsed = 
        try
            let parsed = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
            Some parsed
        with e ->
            printfn $"%s{e.Message}"
            None
    
    match parsed with
    | None -> 2
    | Some parsed ->
        
        let target = parsed.GetSubCommand().StepName
        Targets.Setup parsed
        
        let swallowTypes = [typeof<ProcExecException>; typeof<ExceptionExiter>]
        let shortErrorsFor = (fun e -> swallowTypes |> List.contains (e.GetType()) )
        
        async {
            do! Async.SwitchToThreadPool ()
            return! Targets.RunTargetsAndExitAsync([target], shortErrorsFor, fun _ -> ":") |> Async.AwaitTask
        } |> Async.RunSynchronously
        
        0
        
