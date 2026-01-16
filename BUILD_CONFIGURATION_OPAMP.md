# Build and Packaging Configuration for OpAmp Isolation

## Overview
The AutoInstrumentation package uses conditional compilation to enable OpAmp isolation (via AssemblyLoadContext) only on frameworks that support it.

## Configuration

### Project File Settings (AutoInstrumentation.csproj)

```xml
<!-- Define USE_ISOLATED_OPAMP_CLIENT only for frameworks that support AssemblyLoadContext -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == 'netstandard2.1'">
  <DefineConstants>$(DefineConstants);USE_ISOLATED_OPAMP_CLIENT</DefineConstants>
</PropertyGroup>
```

**Key points:**
- `USE_ISOLATED_OPAMP_CLIENT` is **only** defined for net8.0 and netstandard2.1
- NOT defined for net462 (doesn't support AssemblyLoadContext)
- This ensures no ALC code is compiled for frameworks that don't support it

### Build Script (Packaging.fs)

The redistribute script builds all three target frameworks:

```fsharp
let redistribute (arguments:ParseResults<Build>) =
    exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "net8.0" "-c" "release" }
    exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "netstandard2.1" "-c" "release" }
    exec { run "dotnet" "build" "src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj" "-f" "net462" "-c" "release" }
```

**Why three frameworks?**
- **net8.0**: Recommended for modern .NET applications
- **netstandard2.1**: For compatibility with legacy projects
- **net462**: For .NET Framework applications; no ALC isolation (not supported)

### Conditional Compilation Flow

```
┌─────────────────────────────────────────────────────────┐
│ dotnet build (called by redistribute)                   │
└──────────────────────┬──────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
    net8.0      netstandard2.1     net462
        │              │              │
   USE_ISO=YES   USE_ISO=YES     USE_ISO=NO
        │              │              │
  [ALC code     [ALC code    [No ALC code
   compiled]    compiled]     - fallback]
```

## Runtime Behavior

### net8.0 and netstandard2.1
When `USE_ISOLATED_OPAMP_CLIENT` is defined:
- OpAmp and Protobuf are loaded in isolated AssemblyLoadContext
- Prevents version conflicts with application dependencies
- May have minimal reflection overhead

### net462
- `USE_ISOLATED_OPAMP_CLIENT` not defined
- ALC code is excluded at compile time
- Falls back to standard dependency loading
- Potential for version conflicts (but less common on .NET Framework)

## Verification

To verify the build configuration is correct:

1. **Check DefineConstants are applied:**
   ```bash
   dotnet build -f net8.0 -c release -p:DefineConstants=TEST
   ```

2. **Verify ALC code is conditionally compiled:**
   - Check that `IsolatedOpAmpLoadContext.cs` only exists when compiled for net8.0/netstandard2.1
   - Check that `OpAmpIsolationInitializer.cs` has proper `#if` guards

3. **Verify packaged artifacts:**
   - net8.0 DLL should be ~1-2MB (larger due to ALC code)
   - netstandard2.1 DLL should be ~1-2MB (larger due to ALC code)
   - net462 DLL should be smaller (no ALC code)

## Troubleshooting

### DefineConstants not being applied
- Ensure the PropertyGroup Condition matches the exact TargetFramework value
- Check with: `dotnet build ... /p:DebugSymbols=false /v:d` (verbose output shows DefineConstants)

### ALC code still present on net462
- Verify the `#if` directives in source files match the DefineConstants
- Use `#if NET8_0_OR_GREATER || NETSTANDARD2_1` pattern

### Build fails for redistribute
- Ensure all frameworks in TargetFrameworks have explicit build commands
- Check build/scripts/Packaging.fs has entries for all three frameworks

## Future Considerations

- If support for .NET Framework ALC becomes available, only update the Condition in AutoInstrumentation.csproj
- If net9.0+ support is needed, add to both the Condition and the build script
- Consider adding a build property `<EnableOpAmpIsolation>` to allow per-project override
