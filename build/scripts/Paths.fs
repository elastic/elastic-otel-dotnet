// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

module Paths

open System
open System.IO

let ToolName = "elastic-otel-dotnet"
let Repository = sprintf "elastic/%s" ToolName
let SignKey = "069ca2728db333c1"

let ValidateAssemblyName = false
let IncludeGitHashInInformational = true
let GenerateApiChanges = false

let Root =
    let mutable dir = DirectoryInfo(".")
    while dir.GetFiles("*.sln").Length = 0 do dir <- dir.Parent
    Environment.CurrentDirectory <- dir.FullName
    dir
    
let RootRelative path = Path.GetRelativePath(Root.FullName, path) 
    
let ArtifactPath t = DirectoryInfo(Path.Combine(Root.FullName, ".artifacts", t))
