# Debug script to run ILMerge manually with verbose output
# This helps identify why ILMerge is failing when merging assemblies

param(
    [string]$TargetFramework = "net462",
    [string]$Configuration = "debug"
)

$ilmergePath = "$env:USERPROFILE\.nuget\packages\ilmerge\3.0.41\tools\net452"
$ilmergeExe = "$ilmergePath\ILMerge.exe"

if (!(Test-Path $ilmergeExe)) {
    Write-Error "ILMerge.exe not found at: $ilmergeExe"
    exit 1
}

$artifactPath = ".\.artifacts\bin\Elastic.OpenTelemetry.AutoInstrumentation\${configuration}_${TargetFramework}"

if (!(Test-Path $artifactPath)) {
    Write-Error "Output path not found: $artifactPath"
    exit 1
}

$mainDll = "$artifactPath\Elastic.OpenTelemetry.AutoInstrumentation.dll"
$protobufDll = "$artifactPath\Google.Protobuf.dll"
$opampDll = "$artifactPath\OpenTelemetry.OpAmp.Client.dll"
$mergedDll = "$artifactPath\Elastic.OpenTelemetry.AutoInstrumentation.merged.dll"

Write-Host "ILMerge Debug Script"
Write-Host "==================="
Write-Host "Target Framework: $TargetFramework"
Write-Host "Configuration: $Configuration"
Write-Host ""
Write-Host "Checking assembly files..."
Write-Host "Main DLL exists: $(Test-Path $mainDll)"
Write-Host "Protobuf DLL exists: $(Test-Path $protobufDll)"
Write-Host "OpAmp DLL exists: $(Test-Path $opampDll)"
Write-Host ""

if (!(Test-Path $mainDll)) {
    Write-Error "Main DLL not found: $mainDll"
    exit 1
}

if (!(Test-Path $protobufDll) -or !(Test-Path $opampDll)) {
    Write-Warning "One or more dependency DLLs not found. Skipping merge."
    exit 0
}

Write-Host "Running ILMerge with verbose output..."
Write-Host ""

$ilmergeCmd = @(
    "`"$ilmergeExe`"",
    "/internalize",
    "/targetplatform:v4",
    "/lib:`"$artifactPath`"",
    "/out:`"$mergedDll`"",
    "`"$mainDll`"",
    "`"$protobufDll`"",
    "`"$opampDll`""
)

$command = $ilmergeCmd -join " "
Write-Host "Command: $command"
Write-Host ""

& cmd /c $command

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "ILMerge succeeded!"
    Write-Host "Merged DLL created: $(Test-Path $mergedDll)"
    
    if (Test-Path $mergedDll) {
        Write-Host ""
        Write-Host "Moving merged DLL to main location..."
        Move-Item -Path $mergedDll -Destination $mainDll -Force
        Write-Host "Removing dependency DLLs..."
        Remove-Item -Path $protobufDll -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $opampDll -Force -ErrorAction SilentlyContinue
        Write-Host "Done!"
    }
} else {
    Write-Error "ILMerge failed with exit code: $LASTEXITCODE"
    Write-Host ""
    Write-Host "Troubleshooting steps:"
    Write-Host "1. Check if all transitive dependencies are in: $artifactPath"
    Write-Host "2. Try listing the contents of the dependency DLLs"
    Write-Host "3. Consider using /targetplatform:v2 or /targetplatform:v1 instead of v4"
    exit 1
}
