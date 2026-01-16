# OpAmp Version Conflict Resolution Strategy

## Problem
When `Elastic.OpenTelemetry.AutoInstrumentation` is loaded by the profiler in zero-code instrumentation scenarios, the instrumented application may have its own version of `Google.Protobuf` or `OpenTelemetry.OpAmp.Client`. This causes assembly version conflicts at runtime.

## Solutions Evaluated

### 1. ILMerge (Attempted)
- **Status**: Partially works
- **Limitations**: 
  - Works for net462 with `/internalize` flag
  - Produces corrupted IL on net8.0+ (modern .NET assemblies incompatible with ILMerge 3.0.41)
  - Removed due to IL corruption issues

### 2. AssemblyLoadContext (ALC) Isolation (Recommended)
- **Status**: Recommended approach (not yet fully implemented)
- **How it works**:
  - Load `Google.Protobuf` and `OpenTelemetry.OpAmp.Client` in an isolated `AssemblyLoadContext`
  - Prevents version conflicts by isolating dependencies from the application's versions
  - Uses reflection or facade patterns to interact across ALC boundaries
  
- **Trade-offs**:
  - ✅ Solves version conflicts for all .NET versions (8.0+, netstandard2.1)
  - ✅ No IL corruption
  - ✅ Works with profiler-loaded scenarios
  - ⚠️ Not AoT-compatible (requires reflection)
  - ⚠️ Additional complexity in code

- **Implementation**:
  - Only enabled for `Elastic.OpenTelemetry.AutoInstrumentation`
  - Controlled via `USE_ISOLATED_OPAMP_CLIENT` compiler directive
  - Only activates on net8.0+ (net462 doesn't support AssemblyLoadContext)
  
- **Example Configuration**:
  ```xml
  <!-- In AutoInstrumentation .csproj -->
  <DefineConstants>$(DefineConstants);USE_ISOLATED_OPAMP_CLIENT</DefineConstants>
  ```

### 3. Runtime Assembly Binding Redirects
- **Status**: Not feasible
- **Reason**: Only works for .NET Framework with app.config, not applicable to profiler scenarios

### 4. Package Co-location (Fallback)
- **Status**: Current approach
- **How it works**:
  - Ensure `Google.Protobuf.dll` and `OpenTelemetry.OpAmp.Client.dll` are included in redistributable
  - Modern .NET's assembly resolution can handle multiple versions better than .NET Framework
  
- **Trade-offs**:
  - ✅ Simple, no code changes
  - ⚠️ Not guaranteed to prevent version conflicts
  - ⚠️ Relies on runtime behavior

## Recommendation

For `Elastic.OpenTelemetry.AutoInstrumentation`:
1. **Implement ALC-based isolation** for net8.0+ 
2. **Accept non-AoT compatibility** for this package only
   - AutoInstrumentation already has AoT limitations (profiler-loaded)
   - Main `Elastic.OpenTelemetry` package can remain AoT-compatible
3. **Use compiler directives** to control isolation per-package
4. **Provide clear documentation** about AoT limitations when using OpAmp in AutoInstrumentation

## Current Status

- ✅ Build succeeds without ILMerge
- ⏳ ALC isolation implementation ready but not yet integrated
- ✅ AutoInstrumentation project configured for isolated loading
- ⏳ Integration with Central Configuration pending

## Future Work

1. Integrate ALC-based OpAmp loading in AutoInstrumentation's `ElasticOpenTelemetry.Bootstrap()`
2. Add configuration option to enable/disable isolation per package
3. Document AoT compatibility limitations
4. Consider facade pattern for cleaner ALC boundary crossing
