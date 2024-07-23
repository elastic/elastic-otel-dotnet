// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information


module Packaging

open System
open System.IO
open System.Net.Http
open Argu
open CommandLine
open Octokit

let downloadArtifacts (arguments:ParseResults<Build>) =
    let client = GitHubClient(ProductHeaderValue("Elastic.OpenTelemetry"))
    let token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    if not(String.IsNullOrWhiteSpace(token)) then 
        Console.WriteLine($"using GITHUB_TOKEN");
        let tokenAuth = Credentials(token); // This can be a PAT or an OAuth token.
        client.Credentials <- tokenAuth
    //https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/releases/tag/v1.7.0
    
    let downloadFolder = Path.Combine(".artifacts", "otel-distribution") |> Directory.CreateDirectory
    
    let assets =
        async {
            let! release = client.Repository.Release.Get("open-telemetry", "opentelemetry-dotnet-instrumentation", "v1.7.0") |> Async.AwaitTask;
            Console.WriteLine($"Release %s{release.Name} has %i{release.Assets.Count} assets");
            return release.Assets
        } |> Async.RunSynchronously
    
    async {
        use httpClient = new HttpClient()
        assets
        |> Seq.map (fun asset -> (asset, Path.Combine(downloadFolder.FullName, asset.Name)))
        |> Seq.filter (fun (_, path) -> not <| File.Exists path)
        |> Seq.iter (fun (asset, path) ->
            async {
                Console.WriteLine($"Retrieving {asset.Name}");
                let! fileData = httpClient.GetByteArrayAsync(asset.BrowserDownloadUrl) |> Async.AwaitTask
                Console.WriteLine($"Saveing %i{fileData.Length} bytes to {path}")
                File.WriteAllBytes(path, fileData)
            } |> Async.RunSynchronously
        )
    } |> Async.RunSynchronously