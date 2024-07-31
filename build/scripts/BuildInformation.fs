// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module BuildInformation

open System
open System.IO
open System.Threading
open System.Xml.Linq
open System.Xml.XPath
open Fake.Core
open Proc.Fs
open Fake.Tools.Git

type Paths =
    static member Root =
        let mutable dir = DirectoryInfo(".")
        while dir.GetFiles("*.sln").Length = 0 do dir <- dir.Parent
        Environment.CurrentDirectory <- dir.FullName
        dir
        
    static member RelativePathToRoot path = Path.GetRelativePath(Paths.Root.FullName, path) 
        
    static member ArtifactFolder = DirectoryInfo(Path.Combine(Paths.Root.FullName, ".artifacts"))
    static member ArtifactPath t = DirectoryInfo(Path.Combine(Paths.ArtifactFolder.FullName, t))
    
    static member private SrcFolder = DirectoryInfo(Path.Combine(Paths.Root.FullName, "src"))
    static member SrcPath (t: string list) = DirectoryInfo(Path.Combine([Paths.SrcFolder.FullName] @ t |> List.toArray))


type BuildConfiguration = 
    static member ValidateAssemblyName = false
    static member GenerateApiChanges = false
    
        
        

type Software =
    static member Organization = "elastic"
    static member Repository = "elastic-otel-dotnet"
    static member GithubMoniker = $"%s{Software.Organization}/%s{Software.Repository}"
    static member SignKey = "069ca2728db333c1"
    
    static let queryPackageRef upstreamPackage distroPackage =
        let path = Paths.SrcPath [distroPackage; $"{distroPackage}.csproj"]
        let project = XDocument.Load(path.FullName)
        let packageRef = project.XPathSelectElement($"//PackageReference[@Include = '{upstreamPackage}']")
        let upstreamVersion = packageRef.Attribute("Version").Value
        SemVer.parse(upstreamVersion)

    static let restore =
        Lazy<unit>((fun _ -> exec { run "dotnet" "tool" "restore" }), LazyThreadSafetyMode.ExecutionAndPublication)
        
    static let versionInfo =
        Lazy<SemVerInfo>(fun _ ->
            let sha = Information.getCurrentSHA1 "."
            let output = exec {
                binary "dotnet"
                arguments "minver" "-p" "canary.0" "-m" "0.1"
                find (fun l -> not(l.Error))
            }
            SemVer.parse <| $"%s{output.Line}+%s{sha}"
        , LazyThreadSafetyMode.ExecutionAndPublication)
        
    static member Version = restore.Value; versionInfo.Value
    
    static member OpenTelemetryAutoInstrumentationVersion =
        let upstreamPackage = "OpenTelemetry.AutoInstrumentation";
        let distroPackage = $"Elastic.{upstreamPackage}"
        queryPackageRef upstreamPackage distroPackage
        
    static member OpenTelemetryVersion =
        let upstreamPackage = "OpenTelemetry";
        let distroPackage = $"Elastic.{upstreamPackage}"
        queryPackageRef upstreamPackage distroPackage
        

type OS =
    | OSX | Windows | Linux
with
    static member Current = 
        match int Environment.OSVersion.Platform with
        | 4 | 128 -> Linux
        | 6       -> OSX
        | _       -> Windows

