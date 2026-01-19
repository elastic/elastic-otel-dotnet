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
open Proc.Fs

///
/// This module is hard to read, with so many file manipulations it's kinda hard to avoid.
///
/// Packaging ultimately ensures artifacts are available under .artifacts/elastic-distribution/{version}
///
/// - Download release assets from: github.com/open-telemetry/opentelemetry-dotnet-instrumentation/{version}
///     - cached under .artifacts/otel-distribution/{version}, only downloaded again if an asset is missing
/// - Staging copies are created in the `otel-distribution`, renaming `opentelemetry-` to `staging-`
///     - these copies are used to do mutations and removed afterward
/// - Zip mutations
///     - Ensure we package our plugin dll and related files under net/netfx inside the staging zips.
///     - Ensure we rename `instrument.sh` to `_instrument.sh` inside the scripts
///     - Include our wrapping `instrument.sh` as the new main entry script.
/// - Move mutated staging zips `.artifacts/elastic-distribution/{version}` renaming `stage-` to `elastic-`.
/// 

let private otelAutoVersion = Software.OpenTelemetryAutoInstrumentationVersion;

let private downloadFolder = Path.Combine(".artifacts", "otel-distribution", otelAutoVersion.AsString) |> Directory.CreateDirectory
let private distroFolder = Path.Combine(".artifacts", "elastic-distribution") |> Directory.CreateDirectory

let private fileInfo (directory: DirectoryInfo) file = Path.Combine(directory.FullName, file) |> FileInfo
let private downloadFileInfo (file: string) = fileInfo downloadFolder file
let private downloadAsset (asset: ReleaseAsset) = fileInfo downloadFolder asset.Name

let private _stage (s: string) = s.Replace("opentelemetry", "stage").Replace("otel", "stage").Replace("OpenTelemetry", "stage")
let private stageFile (file: FileInfo) = fileInfo downloadFolder (file.Name |> _stage)
let private stageAsset (asset: ReleaseAsset) = fileInfo downloadFolder (asset.Name |> _stage)

let private _distro (s: string) = (s |> _stage).Replace("stage", "elastic")
let private distroFile (file: FileInfo) = fileInfo distroFolder (file.Name |> _distro)
let private distroAsset (asset: ReleaseAsset) = fileInfo distroFolder (asset.Name |> _distro)

let pluginFiles tfm =
    ["dll"; "pdb"; "xml"]
    |> List.map(fun e -> $"Elastic.OpenTelemetry.AutoInstrumentation.%s{e}")
    |> List.map(fun f -> Path.Combine(".artifacts", "bin", "Elastic.OpenTelemetry.AutoInstrumentation", $"release_{tfm}", "", f))
    |> List.map(fun f -> FileInfo(f))

/// Additional files needed for OpAmp abstractions layer (NET8+ only)
let opAmpAbstractionsFiles tfm =
    if tfm = "net8.0" then
        ["dll"; "pdb"]
        |> List.map(fun e -> $"Elastic.OpenTelemetry.OpAmp.Abstractions.%s{e}")
        |> List.map(fun f -> Path.Combine(".artifacts", "bin", "Elastic.OpenTelemetry.OpAmp.Abstractions", $"release_{tfm}", "", f))
        |> List.map(fun f -> FileInfo(f))
    else
        []

/// OpenTelemetry.OpAmp.Client and its dependencies (needed for isolated ALC loading)
let opAmpDependencyFiles tfm =
    if tfm = "net8.0" then
        [
            "OpenTelemetry.OpAmp.Client"
            "Google.Protobuf"
        ]
        |> List.collect(fun pkg ->
            ["dll"; "pdb"]
            |> List.map(fun e -> 
                let pkgPath = Path.Combine(".artifacts", "bin", "OpenTelemetry.OpAmp.Client", "release_{tfm}", "")
                Path.Combine(pkgPath, $"%s{pkg}.%s{e}")
            )
        )
        |> List.map(fun f -> FileInfo(f))
        |> List.filter(fun f -> f.Exists) // Only include files that actually exist
    else
        []

let downloadArtifacts (_:ParseResults<Build>) =
    let client = GitHubClient(ProductHeaderValue "Elastic.OpenTelemetry")
    let token = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
    if not(String.IsNullOrWhiteSpace(token)) then 
        printfn "using GITHUB_TOKEN";
        let tokenAuth = Credentials(token); 
        client.Credentials <- tokenAuth
    
    let assets =
        async {
            let! release = client.Repository.Release.Get("open-telemetry", 
                "opentelemetry-dotnet-instrumentation", $"v{otelAutoVersion.AsString}") |> Async.AwaitTask;
            Console.WriteLine $"Release %s{release.Name} has %i{release.Assets.Count} assets";
            return release.Assets
                |> Seq.map (fun asset -> asset, downloadAsset asset)
                |> Seq.toList
        } |> Async.RunSynchronously
    
    async {
        use httpClient = new HttpClient()
        assets
        |> Seq.filter (fun (_, f) -> not f.Exists)
        |> Seq.iter (fun (asset, f) ->
            async {
                Console.WriteLine $"Retrieving {asset.Name}";
                let! fileData = httpClient.GetByteArrayAsync asset.BrowserDownloadUrl |> Async.AwaitTask
                Console.WriteLine $"Saving %i{fileData.Length} bytes to {f.FullName}"
                File.WriteAllBytes(f.FullName, fileData)
                f.Refresh()
            } |> Async.RunSynchronously
        )
    } |> Async.RunSynchronously
    assets

let injectPluginFiles (asset: ReleaseAsset) (stagedZip: FileInfo) tfm target  = 
    use zipArchive = ZipFile.Open(stagedZip.FullName, ZipArchiveMode.Update)
    
    // Inject main plugin files
    pluginFiles tfm  |> List.iter(fun f ->
        printfn $"Staging zip: %s{stagedZip.Name}, Adding: %s{f.Name} (%s{tfm}) to %s{target}"
        zipArchive.CreateEntryFromFile(f.FullName, Path.Combine(target, f.Name)) |> ignore
    )
    
    // Inject OpAmp abstractions files (NET8+ only)
    opAmpAbstractionsFiles tfm |> List.iter(fun f ->
        if f.Exists then
            printfn $"Staging zip: %s{stagedZip.Name}, Adding OpAmp abstraction: %s{f.Name} (%s{tfm}) to %s{target}"
            zipArchive.CreateEntryFromFile(f.FullName, Path.Combine(target, f.Name)) |> ignore
        else
            printfn $"Warning: OpAmp abstraction file not found: %s{f.FullName}"
    )
    
    // Inject OpAmp dependency files (NET8+ only)
    opAmpDependencyFiles tfm |> List.iter(fun f ->
        if f.Exists then
            printfn $"Staging zip: %s{stagedZip.Name}, Adding OpAmp dependency: %s{f.Name} (%s{tfm}) to %s{target}"
            zipArchive.CreateEntryFromFile(f.FullName, Path.Combine(target, f.Name)) |> ignore
        else
            printfn $"Warning: OpAmp dependency file not found: %s{f.FullName}"
    )

let injectPluginScripts (stagedZip: FileInfo) (otelScript: FileInfo) (script: FileInfo) = 
    use zipArchive = ZipFile.Open(stagedZip.FullName, ZipArchiveMode.Update)
    
    printfn $"Staging : %s{stagedZip.Name}, Adding: %s{otelScript.Name}"
    zipArchive.CreateEntryFromFile(otelScript.FullName, otelScript.Name) |> ignore
    
    printfn $"Staging : %s{stagedZip.Name}, Adding: %s{script.FullName}"
    let entry = zipArchive.Entries |> Seq.find(fun e -> e.Name = "instrument.sh")
    entry.Delete()
    
    zipArchive.CreateEntryFromFile(script.FullName, script.Name) |> ignore

let stageInstrumentationScript (stagedZips:List<ReleaseAsset * FileInfo>) =
    let openTelemetryVersion = downloadFileInfo "opentelemetry-instrument.sh"
    let stageVersion = downloadFileInfo "_instrument.sh"
    let stageScript =
        match stageVersion.Exists with
        | true -> stageVersion
        | _ -> 
            let instrumentShZip =
                stagedZips
                |> List.map(fun (_, p) -> p)
                |> List.find (fun p -> not <| p.Name.EndsWith "-windows.zip")
            use zipArchive = ZipFile.Open(instrumentShZip.FullName, ZipArchiveMode.Read)
            let shArchive = zipArchive.Entries |> Seq.find(fun e -> e.Name = "instrument.sh")
            shArchive.ExtractToFile openTelemetryVersion.FullName
            openTelemetryVersion.Refresh()
            openTelemetryVersion.MoveTo stageVersion.FullName
            stageVersion.Refresh()
            stageVersion
        
    let wrapperScript = downloadFileInfo "instrument.sh"
    let copyScript = Path.Combine("src", "Elastic.OpenTelemetry.AutoInstrumentation", "instrument.sh") |> FileInfo
    let script = copyScript.CopyTo(wrapperScript.FullName, true)
    (stageScript, script)

let stageInstallationBashScript () =
    let installScript = downloadFileInfo "otel-dotnet-auto-install.sh"
    let staged = installScript.CopyTo ((stageFile installScript).FullName, true)
    let contents =
        (File.ReadAllText staged.FullName)
            .Replace("/open-telemetry/opentelemetry-dotnet-instrumentation/", "/elastic/elastic-otel-dotnet/")
            .Replace("opentelemetry-dotnet-instrumentation", "elastic-dotnet-instrumentation")
            .Replace("v" + Software.OpenTelemetryAutoInstrumentationVersion.AsString, Software.Version.Normalize())
            
    let elasticInstall = distroFile installScript
    File.WriteAllText(elasticInstall.FullName, contents)

    if not (OperatingSystem.IsWindows()) then
        let permissions =
            UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
            ||| UnixFileMode.GroupRead ||| UnixFileMode.GroupWrite ||| UnixFileMode.GroupExecute
            ||| UnixFileMode.OtherRead ||| UnixFileMode.OtherWrite ||| UnixFileMode.OtherExecute
        File.SetUnixFileMode(elasticInstall.FullName, permissions);

let stageInstallationPsScript () =
    let installScript = downloadFileInfo "OpenTelemetry.DotNet.Auto.psm1"
    let staged = installScript.CopyTo ((stageFile installScript).FullName, true)
   
    let envMarker = "\"OTEL_DOTNET_AUTO_HOME\"               = $OTEL_DOTNET_AUTO_HOME;"
    let contents =
        (File.ReadAllText staged.FullName)
            .Replace("/open-telemetry/opentelemetry-dotnet-instrumentation/", "/elastic/elastic-otel-dotnet/")
            .Replace("opentelemetry-dotnet-instrumentation", "elastic-dotnet-instrumentation")
            .Replace("OpenTelemetry .NET Automatic Instrumentation", "Elastic Distribution of OpenTelemetry (EDOT) .NET")
            .Replace("OpenTelemetry.DotNet.Auto", "Elastic.OpenTelemetry.DotNet")
            .Replace(envMarker,
                     [
                        envMarker
                        "#Elastic Distribution"
                        "\"OTEL_DOTNET_AUTO_PLUGINS\"            = \"Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation\""
                     ]
                     |> String.concat "\r\n        "
            )
            .Replace("v" + Software.OpenTelemetryAutoInstrumentationVersion.AsString, Software.Version.Normalize())
    let elasticInstall = distroFile installScript
    //ensure we write our new module name
    File.WriteAllText(elasticInstall.FullName.Replace("elastic.DotNet.Auto", "Elastic.OpenTelemetry.DotNet"), contents);

/// moves artifacts from open-distribution to elastic-distribution and renames them to `staged-dotnet-instrumentation*`.
/// staged meaning we haven't injected our opentelemetry dll into the zip yet,
let stageArtifacts (assets:List<ReleaseAsset * FileInfo>) =
    let stagedZips =
        assets
        |> List.filter(fun (a, _) -> a.Name.EndsWith ".zip")
        |> List.filter(fun (a, _) -> not <| a.Name.EndsWith "nuget-packages.zip")
        |> List.map(fun (z, f) ->
            let stage = stageAsset z
            z, f.CopyTo(stage.FullName, true)
        )
    
    let otelScript, wrapperScript = stageInstrumentationScript stagedZips
    printfn $"Staged (%s{wrapperScript.Name}) calling into (%s{otelScript.Name} for repackaging)"
        
    stagedZips |> List.iter (fun (asset, path) ->
        
        // We inject net8.0 as the minimum supported TFM version
        // Previously we used netstandard2.1, but this causes issues with adding a handler for the HttpClient
        // used for OTLP export as the SDK prefers the `Send` rather than `SendAsync` method which is only available in net8.0+ TFM
        // Whe using netstandard2.1 there is no handler code to run so the default user agent is sent.
        injectPluginFiles asset path "net8.0" "net"
        if asset.Name.EndsWith "-windows.zip" then
            injectPluginFiles asset path "net462" "netfx"
            
        injectPluginScripts path otelScript wrapperScript
        
        let distro = distroAsset asset
        path.MoveTo(distro.FullName, true)
        distro.Refresh()
        
        printfn $"Moved staging to: %s{distro.FullName}"
    )
    stagedZips

let redistribute (arguments:ParseResults<Build>) =
    // We build net8.0 as the minimum supported TFM version - See above for details
    exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "net8.0" "-c" "release" }
    // exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "netstandard2.1" "-c" "release" }
    exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "net462" "-c" "release" }
    let assets = downloadArtifacts arguments
    printfn ""
    assets |> List.iter (fun (asset, path) ->
        printfn "Asset: %s" asset.Name
    )
    stageInstallationBashScript()
    stageInstallationPsScript()
    let staged = stageArtifacts assets
    ignore()
