// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information


module Packaging

open System
open System.IO
open System.IO.Compression
open System.Net.Http
open Argu
open BuildInformation
open CommandLine
open Octokit

let private otelAutoVersion = BuildConfiguration.OpenTelemetryAutoInstrumentationVersion;

let private downloadFolder = Path.Combine(".artifacts", "otel-distribution", otelAutoVersion.AsString) |> Directory.CreateDirectory
let private distroFolder = Path.Combine(".artifacts", "elastic-distribution", otelAutoVersion.AsString) |> Directory.CreateDirectory

let private fileInfo (directory: DirectoryInfo) file = Path.Combine(directory.FullName, file) |> FileInfo
let private downloadFile (asset: ReleaseAsset) = fileInfo downloadFolder asset.Name
let private stageFile (asset: ReleaseAsset) = fileInfo downloadFolder (asset.Name.Replace("opentelemetry", "stage"))
let private distroFile (asset: ReleaseAsset) = fileInfo distroFolder (asset.Name.Replace("opentelemetry", "elastic"))

let pluginFiles tfm =
    ["dll"; "pdb"; "xml"]
    |> List.map(fun e -> $"Elastic.OpenTelemetry.%s{e}")
    |> List.map(fun f -> Path.Combine(".artifacts", "bin", "Elastic.OpenTelemetry", $"release_%s{tfm}", "", f))
    |> List.map(fun f -> FileInfo(f))
    

/// downloads the artifacts if they don't already exist locally
let downloadArtifacts (_:ParseResults<Build>) =
    let client = GitHubClient(ProductHeaderValue("Elastic.OpenTelemetry"))
    let token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    if not(String.IsNullOrWhiteSpace(token)) then 
        Console.WriteLine($"using GITHUB_TOKEN");
        let tokenAuth = Credentials(token); 
        client.Credentials <- tokenAuth
    
    let assets =
        async {
            let! release = client.Repository.Release.Get("open-telemetry", "opentelemetry-dotnet-instrumentation", $"v{otelAutoVersion.AsString}") |> Async.AwaitTask;
            Console.WriteLine($"Release %s{release.Name} has %i{release.Assets.Count} assets");
            return release.Assets
                |> Seq.map (fun asset -> (asset, downloadFile asset))
                |> Seq.toList
        } |> Async.RunSynchronously
    
    async {
        use httpClient = new HttpClient()
        assets
        |> Seq.filter (fun (_, f) -> not f.Exists)
        |> Seq.iter (fun (asset, f) ->
            async {
                Console.WriteLine($"Retrieving {asset.Name}");
                let! fileData = httpClient.GetByteArrayAsync(asset.BrowserDownloadUrl) |> Async.AwaitTask
                Console.WriteLine($"Saveing %i{fileData.Length} bytes to {f.FullName}")
                File.WriteAllBytes(f.FullName, fileData)
                f.Refresh()
            } |> Async.RunSynchronously
        )
    } |> Async.RunSynchronously
    assets

let injectPluginFiles (asset: ReleaseAsset) (stagedZip: FileInfo) tfm target  = 
    use zipArchive = ZipFile.Open(stagedZip.FullName, ZipArchiveMode.Update)
    pluginFiles tfm  |> List.iter(fun f ->
        printfn $"Staging zip: %s{asset.Name}, Adding: %s{f.Name} (%s{tfm}_ to %s{target}"
        zipArchive.CreateEntryFromFile(f.FullName, Path.Combine(target, f.Name)) |> ignore
    )
    
/// moves artifacts from open-distribution to elastic-distribution and renames them to `staged-dotnet-instrumentation*`.
/// staged meaning we haven't injected our opentelemetry dll into the zip yet,
let stageArtifacts (assets:List<ReleaseAsset * FileInfo>) =
    let stagedZips =
        assets
        |> List.filter(fun (a, _) -> a.Name.EndsWith(".zip"))
        |> List.map(fun (z, f) ->
            let stage = stageFile z
            z, f.CopyTo(stage.FullName, true)
        )
    
    stagedZips |> List.iter (fun (asset, path) ->
        
        injectPluginFiles asset path "netstandard2.1" "net"
        if asset.Name.EndsWith("-windows.zip") then
            injectPluginFiles asset path "net462" "netfx"
        
        let distro = distroFile asset
        path.MoveTo(distro.FullName, true)
        distro.Refresh()
        
        printfn $"Created: %s{distro.FullName}"
    )
    stagedZips
    
    
  
let redistribute (arguments:ParseResults<Build>) =
    let assets = downloadArtifacts arguments
    let staged = stageArtifacts assets
        
    printfn ""
    assets |> List.iter (fun (asset, path) ->
        printfn "Asset: %s" asset.Name
    )
    
    
