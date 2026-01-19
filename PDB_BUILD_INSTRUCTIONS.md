# PDB Build Synchronization Instructions

## Problem
PDB (Program Database) files are out of sync with the compiled code, causing line numbers and source locations in stack traces not to match the current source code.

## Solution

### 1. Clean Build (Complete Cache Flush)

Run the following commands in PowerShell from the repository root:

```powershell
# Remove all build artifacts
dotnet clean

# Remove NuGet cache if needed (optional but recommended)
Remove-Item -Path $env:USERPROFILE\.nuget\packages -Recurse -Force -ErrorAction SilentlyContinue

# Remove bin and obj directories recursively
Get-ChildItem -Path . -Include bin,obj -Recurse | Remove-Item -Recurse -Force
```

### 2. Rebuild with PDB Generation

```powershell
# Restore dependencies
dotnet restore

# Build Release with explicit PDB settings
dotnet build -c Release --no-restore --no-incremental
```

### 3. Verify PDB Files Generated

```powershell
# Check for PDB files in the output directory
Get-ChildItem -Path .\src\Elastic.OpenTelemetry.AutoInstrumentation\bin\Release -Include *.pdb -Recurse

# Should show:
# - Elastic.OpenTelemetry.AutoInstrumentation.pdb
# - Elastic.OpenTelemetry.Core.pdb
# And other dependencies
```

### 4. Update AutoInstrumentation Installation

1. **Locate the installed files:**
   - Default: `C:\Program Files\OpenTelemetry .NET AutoInstrumentation\net`
   
2. **Backup existing files** (recommended):
   ```powershell
   $backupPath = "C:\Program Files\OpenTelemetry .NET AutoInstrumentation\net\backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
   New-Item -ItemType Directory -Path $backupPath -Force
   Copy-Item -Path "C:\Program Files\OpenTelemetry .NET AutoInstrumentation\net\*.dll" -Destination $backupPath
   ```

3. **Copy new binaries with PDBs:**
   ```powershell
   # For net8.0 builds
   Copy-Item -Path ".\src\Elastic.OpenTelemetry.AutoInstrumentation\bin\Release\net8.0\Elastic.*.dll" `
             -Destination "C:\Program Files\OpenTelemetry .NET AutoInstrumentation\net" -Force
   
   Copy-Item -Path ".\src\Elastic.OpenTelemetry.AutoInstrumentation\bin\Release\net8.0\Elastic.*.pdb" `
             -Destination "C:\Program Files\OpenTelemetry .NET AutoInstrumentation\net" -Force
   ```

### 5. Clear Application Cache

```powershell
# Clear .NET runtime cache
Remove-Item -Path "$env:LOCALAPPDATA\Microsoft\dotnet" -Recurse -Force -ErrorAction SilentlyContinue

# If using NGen (Native Image Generator), clear ngen cache
ngen executeQueuedItems
```

## Project File Configuration

The following has been added to both project files to ensure PDB generation:

```xml
<PropertyGroup>
  <!-- Embedded PDBs in assemblies for better distribution -->
  <DebugType>embedded</DebugType>
  <DebugSymbols>true</DebugSymbols>
  <!-- Deterministic builds ensure consistent PDB generation -->
  <Deterministic>true</Deterministic>
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DebugType>embedded</DebugType>
  <DebugSymbols>true</DebugSymbols>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
</PropertyGroup>
```

## Key Configuration Explanations

| Setting | Purpose |
|---------|---------|
| `DebugType=embedded` | Embeds PDB information directly in the DLL instead of separate .pdb file |
| `DebugSymbols=true` | Ensures symbols are generated even in Release builds |
| `Deterministic=true` | Makes builds reproducible - same source = same binary/PDB |
| `ContinuousIntegrationBuild=true` | Treats build like CI build, enabling additional optimizations |
| `PublishRepositoryUrl=true` | Embeds repository URL in PDB for source linking |
| `EmbedUntrackedSources=true` | Embeds all source files in PDB, even those not in Git |

## Verification

After deployment, verify PDB synchronization:

```csharp
// Check if line numbers match in exception stack traces
try 
{
    throw new Exception("Test");
}
catch (Exception ex)
{
    var frames = new StackTrace(ex, true).GetFrames();
    foreach (var frame in frames)
    {
        Console.WriteLine($"{frame.GetMethod()?.Name} - Line {frame.GetFileLineNumber()}");
    }
}
```

Expected output: Line numbers should match your source code.

## Troubleshooting

### PDB Still Out of Sync

1. **Verify file timestamps:**
   ```powershell
   Get-ChildItem -Path ".\src\Elastic.OpenTelemetry.AutoInstrumentation\bin\Release\net8.0\Elastic.*.dll" | 
   Select-Object Name, LastWriteTime
   ```

2. **Check for locked files:**
   - Close Visual Studio
   - Close any applications using the DLLs
   - Restart IIS (if applicable): `iisreset /restart`

3. **Verify embedded PDB:**
   ```powershell
   # Use pdbstr tool to check if PDB is embedded
   # Install: dotnet tool install pdbstr
   pdbstr -r -s:srcsrv -p:"C:\path\to\assembly.dll"
   ```

### Source Code Still Not Found

1. **Check source path in PDB:**
   - Visual Studio: Debug → Windows → Modules
   - Right-click assembly → Symbol Loaded Information
   - Verify source paths match

2. **Configure symbol cache:**
   - Tools → Options → Debugging → Symbols
   - Set local symbol cache directory

## CI/CD Pipeline

If building in CI/CD, ensure these MSBuild parameters are used:

```bash
dotnet build -c Release \
  -p:DebugType=embedded \
  -p:DebugSymbols=true \
  -p:Deterministic=true \
  -p:ContinuousIntegrationBuild=true
```

## References

- [Microsoft Docs: Embedded PDB Support](https://github.com/dotnet/designs/blob/master/accepted/2017/embedding-pdb.md)
- [Deterministic Builds](https://docs.microsoft.com/en-us/dotnet/core/tools/deterministic-builds)
- [Symbol Files (PDB) in .NET](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/symbols)
