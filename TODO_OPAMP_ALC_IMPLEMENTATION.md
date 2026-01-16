# TODO: Complete OpAmp ALC Isolation Implementation

## Current Status
✅ Project configuration set up for conditional ALC compilation  
✅ Build script updated to build all three frameworks  
✅ Compiler directives properly scoped to frameworks that support ALC  
⏳ **PENDING**: Actual ALC implementation code files

## Required Implementation

### 1. Create `IsolatedOpAmpLoadContext.cs`
**Location**: `src/Elastic.OpenTelemetry.Core/Configuration/IsolatedOpAmpLoadContext.cs`

**Purpose**: Custom AssemblyLoadContext for isolating OpAmp and Protobuf dependencies

**Key requirements**:
- Only compile for net8.0+ and netstandard2.1 (`#if NET8_0_OR_GREATER || NETSTANDARD2_1`)
- Load `Google.Protobuf` and `OpenTelemetry.OpAmp.Client` assemblies
- Prevent resolution from the default ALC (isolate from application versions)
- Use `AppContext.BaseDirectory` instead of `Assembly.Location` for AoT compatibility

**Template structure**:
```csharp
#if NET8_0_OR_GREATER || NETSTANDARD2_1

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

internal sealed class IsolatedOpAmpLoadContext : AssemblyLoadContext
{
    private static IsolatedOpAmpLoadContext? _instance;
    private static readonly object LockObject = new();
    private readonly string _basePath;

    // GetOrCreate(string? basePath) - singleton factory
    // Load(AssemblyName) - override to intercept OpAmp/Protobuf
}

#endif
```

### 2. Create `OpAmpIsolationInitializer.cs`
**Location**: `src/Elastic.OpenTelemetry.Core/Configuration/OpAmpIsolationInitializer.cs`

**Purpose**: Initialize the isolated context at startup

**Key requirements**:
- Call from `CentralConfiguration` constructor before OpAmpClient is instantiated
- Pre-load assemblies into isolated context
- Graceful fallback if isolation fails
- Frame-specific: fallback for frameworks without ALC support

**Integration point**: Call in `CentralConfiguration.ctor` before `new OpAmpClient(...)`

### 3. Update `CentralConfiguration.cs`
**Location**: `src/Elastic.OpenTelemetry.Core/Configuration/CentralConfiguration.cs`

**Change**:
```csharp
internal CentralConfiguration(CompositeElasticOpenTelemetryOptions options, CompositeLogger logger)
{
    // ... existing code ...
    
    // Initialize isolated loading for OpAmp dependencies
    OpAmpIsolationInitializer.Initialize();
    
    // Now create OpAmpClient (will use isolated context if available)
    _client = new OpAmpClient(opts => { ... });
    
    // ... rest of code ...
}
```

## Implementation Considerations

### AssemblyLoadContext Behavior
- When an assembly is loaded in an ALC, its dependencies are resolved in that same context by default
- This means once `OpenTelemetry.OpAmp.Client` is loaded in the isolated context, its reference to `Google.Protobuf` will be satisfied by the version in that context
- Application's `Google.Protobuf` remains in the default ALC, preventing version conflicts

### Error Handling
- If isolation fails (rare), should fall back gracefully to default loading
- Log warnings but don't fail startup
- Allow OpAmp to work without isolation as fallback

### Performance Impact
- Minimal: only one-time cost at initialization
- No runtime overhead after initialization
- ALC resolution is optimized in modern .NET

### AoT Compatibility Notes
- Don't use `Assembly.Location` (returns empty in single-file apps)
- Use `AppContext.BaseDirectory` instead
- Expect IL2026 warnings about reflection (acceptable for this component)
- Already non-AoT due to profiler loading, so acceptable tradeoff

## Testing Required

1. **Unit tests**: Verify ALC is created and assemblies are loaded
2. **Integration tests**: 
   - Verify no `FileNotFoundException` when app has conflicting versions
   - Verify OpAmp functionality works with isolation
3. **Framework-specific tests**:
   - net8.0: Should use isolated context
   - netstandard2.1: Should use isolated context  
   - net462: Should fallback (no ALC support)

## Files to Create/Modify

| File | Status | Notes |
|------|--------|-------|
| `IsolatedOpAmpLoadContext.cs` | ⏳ TO DO | New file, only compile for net8.0+ |
| `OpAmpIsolationInitializer.cs` | ⏳ TO DO | New file, with framework-specific fallback |
| `CentralConfiguration.cs` | ⏳ MODIFY | Add one-line initialization call |
| `AutoInstrumentation.csproj` | ✅ DONE | DefineConstants properly configured |
| `Packaging.fs` | ✅ DONE | Build script includes all frameworks |

## Estimated Effort
- Implementation: ~2-3 hours
- Testing: ~2-3 hours  
- Documentation updates: ~1 hour

## Branch/PR Notes
- Currently on branch: `opamp-client-v2`
- Configuration is ready, just needs implementation code
- No changes to public API
- Fully backward compatible
