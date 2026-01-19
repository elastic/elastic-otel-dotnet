# Build Process for OpAmp Abstractions Assembly

## Overview

The `Elastic.OpenTelemetry.OpAmp.Abstractions` assembly is built as part of the normal build process but is only packaged into the OpenTelemetry AutoInstrumentation redistributable when running `build.bat redistributable`.

## Build Workflow

### 1. Local Development Build
```bash
dotnet build
```

Produces:
- `.artifacts/bin/Elastic.OpenTelemetry.OpAmp.Abstractions/release_net8.0/Elastic.OpenTelemetry.OpAmp.Abstractions.dll`
- `.artifacts/bin/Elastic.OpenTelemetry.OpAmp.Abstractions/release_net8.0/Elastic.OpenTelemetry.OpAmp.Abstractions.pdb`

### 2. Redistributable Build
```bash
build.bat redistributable
```

The build script (`build/scripts/Packaging.fs`) performs the following steps:

1. **Downloads OpenTelemetry AutoInstrumentation assets** from GitHub
2. **Stages the downloaded zips** for modification
3. **Injects plugin files** (runs for NET8.0 only):
   - `Elastic.OpenTelemetry.AutoInstrumentation.{dll,pdb,xml}`
   - `Elastic.OpenTelemetry.OpAmp.Abstractions.{dll,pdb}` (NEW)
   - `OpenTelemetry.OpAmp.Client.{dll,pdb}` (NEW)
   - `Google.Protobuf.{dll,pdb}` (NEW)
4. **Packages into the net/ folder** of the final redistributable

## File Structure in Redistributable

When `build.bat redistributable` completes, the final redistributable contains:

```
${OTEL_DOTNET_AUTO_INSTALL_DIR}/net/
├── Elastic.OpenTelemetry.AutoInstrumentation.dll
├── Elastic.OpenTelemetry.AutoInstrumentation.pdb
├── Elastic.OpenTelemetry.AutoInstrumentation.xml
├── Elastic.OpenTelemetry.OpAmp.Abstractions.dll          (NEW)
├── Elastic.OpenTelemetry.OpAmp.Abstractions.pdb          (NEW)
├── OpenTelemetry.OpAmp.Client.dll                        (NEW)
├── OpenTelemetry.OpAmp.Client.pdb                        (NEW)
├── Google.Protobuf.dll                                   (NEW)
├── Google.Protobuf.pdb                                   (NEW)
├── ... other files ...
└── scripts/
    └── instrument.sh
```

## Build Script Changes

The `build/scripts/Packaging.fs` file was updated with two new functions:

### `opAmpAbstractionsFiles tfm`
Collects the OpAmp abstractions assembly and PDB files for NET8.0 only:
- `Elastic.OpenTelemetry.OpAmp.Abstractions.dll`
- `Elastic.OpenTelemetry.OpAmp.Abstractions.pdb`

### `opAmpDependencyFiles tfm`
Collects OpAmp runtime dependencies for NET8.0 only:
- `OpenTelemetry.OpAmp.Client.dll`
- `OpenTelemetry.OpAmp.Client.pdb`
- `Google.Protobuf.dll`
- `Google.Protobuf.pdb`

These files are automatically included in the `net/` folder by the existing `injectPluginFiles` function.

### `injectPluginFiles` Updates
The function now:
1. Injects main plugin files (existing behavior)
2. Injects OpAmp abstractions files if they exist (NET8+)
3. Injects OpAmp dependency files if they exist (NET8+)
4. Logs warnings if expected files are missing

## NET 8.0 Only

The OpAmp abstractions assembly is only copied for NET 8.0 because:
- `IOpAmpMessageSubscriber` is designed for NET 8.0+
- Earlier framework versions don't support AssemblyLoadContext
- The abstractions library uses `System.Text.Json` which is best supported in NET 8.0+

The build script checks `if tfm = "net8.0"` to ensure these files are only injected for the NET 8.0 build.

## Environment Variables for Build

When running `build.bat redistributable`:

- **GITHUB_TOKEN** (optional): For authenticated GitHub API requests to download OTEL assets
  - Set if hitting API rate limits
  - Example: `set GITHUB_TOKEN=ghp_xxx...`

## Testing the Build

To verify OpAmp abstractions are properly packaged:

```bash
# Clean build the redistributable
build.bat clean
build.bat redistributable

# Verify files are in the output directory
# Check .artifacts/elastic-distribution/ for the version number, e.g., v1.2.3
dir ".artifacts\elastic-distribution\v*\net\*.OpAmp*.dll"
```

Should show:
- `Elastic.OpenTelemetry.OpAmp.Abstractions.dll`
- `OpenTelemetry.OpAmp.Client.dll`
- `Google.Protobuf.dll`

## Troubleshooting

### Missing OpAmp files in redistributable

1. **Verify build succeeded**: 
   ```bash
   dotnet build -c Release src/Elastic.OpenTelemetry.OpAmp.Abstractions/Elastic.OpenTelemetry.OpAmp.Abstractions.csproj
   ```

2. **Check build artifacts**:
   ```bash
   dir ".artifacts\bin\Elastic.OpenTelemetry.OpAmp.Abstractions\release_net8.0"
   ```

3. **Check for errors in build log**:
   - Look for warnings about missing `.pdb` files
   - Ensure all projects compile in Release mode

### PDB files not included

The build script only includes `.pdb` files if they exist. Ensure PDB generation is enabled in the project properties (already configured in `.csproj` with `<DebugSymbols>true</DebugSymbols>`).

## Dependencies

The abstractions assembly has these NuGet dependencies:
- `OpenTelemetry.OpAmp.Client`
- `System.Text.Json` (built-in for NET 8.0+)

Both are automatically resolved by the build system when needed by `CentralConfiguration`.
