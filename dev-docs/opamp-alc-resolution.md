# OpAmp Assembly Load Context: Resolution Mechanics

This document explains how the `AssemblyDependencyResolver` resolves assemblies for the OpAmp isolated `AssemblyLoadContext` in the redistributable zip distribution, and why shipping the `.deps.json` alongside a flat directory layout works correctly.

## Background

The redistributable zip places all assemblies flat in a `net/` directory:

```
net/
  Elastic.OpenTelemetry.AutoInstrumentation.dll
  Elastic.OpenTelemetry.OpAmp.dll
  Elastic.OpenTelemetry.OpAmp.Abstractions.dll
  Elastic.OpenTelemetry.OpAmp.deps.json
  OpenTelemetry.OpAmp.Client.dll
  Google.Protobuf.dll
  ... (other OTel auto-instrumentation assemblies)
```

The `OpAmpIsolatedLoadContext` creates an `AssemblyDependencyResolver` rooted on `Elastic.OpenTelemetry.OpAmp.dll`. This resolver reads `Elastic.OpenTelemetry.OpAmp.deps.json` to understand the component's dependency graph.

Only three assemblies are loaded into the isolated ALC (controlled by the `AssembliesToLoad` whitelist in `OpAmpIsolatedLoadContext.Load()`):

- `Elastic.OpenTelemetry.OpAmp`
- `OpenTelemetry.OpAmp.Client`
- `Google.Protobuf`

All other assemblies (including `Microsoft.Extensions.Logging.Abstractions`) return `null` from `Load()` and resolve from the default ALC. This preserves type identity for shared types like `ILogger` across the ALC boundary.

## The flat-vs-structured path question

The generated `deps.json` contains NuGet-style structured runtime asset paths for package dependencies:

```json
"Google.Protobuf/3.34.0": {
    "runtime": {
        "lib/net5.0/Google.Protobuf.dll": { ... }
    }
}
"OpenTelemetry.OpAmp.Client/0.1.0-alpha.4": {
    "runtime": {
        "lib/net8.0/OpenTelemetry.OpAmp.Client.dll": { ... }
    }
}
```

But the redistributable zip has these DLLs flat in `net/`, not under `lib/net5.0/` subdirectories. The natural question is: does the resolver try `{net}/lib/net5.0/Google.Protobuf.dll` (which doesn't exist) or `{net}/Google.Protobuf.dll` (which does)?

**Answer: it uses the flat filename.**

## How the runtime resolves component dependencies

`AssemblyDependencyResolver` is a thin managed wrapper. Its constructor calls the native `hostfxr_resolve_component_dependencies` API, which returns fully resolved absolute paths. `ResolveAssemblyToPath()` is then just a dictionary lookup by assembly simple name.

The native resolution in `hostfxr` uses a probe chain. For components, the relevant probe is the "published deps dir" (app-local) probe, which calls `to_dir_path()`.

### `to_dir_path()` extracts only the filename

Source: `src/native/corehost/hostpolicy/deps_entry.cpp` in the dotnet/runtime repository.

For non-resource, non-runtime-pack assets, `to_dir_path()` strips the directory prefix from the asset's relative path and uses only the filename:

```
deps.json asset path: "lib/net5.0/Google.Protobuf.dll"
to_dir_path() extracts: "Google.Protobuf.dll"
Combined with component dir: "{net}/Google.Protobuf.dll"
```

This is by design. `to_dir_path()` is used for app-local probing where assemblies are expected to be flat alongside the component. The structured `to_package_path()` (which preserves `lib/net5.0/...`) is used for NuGet package cache and shared store probing instead.

### Probe order for components

1. **Servicing probes** (if core servicing dir exists) -- uses structured NuGet paths
2. **Published deps dir (app-local)** -- uses `to_dir_path()` = **flat filename**
3. **Shared store probes** -- uses structured NuGet paths
4. **Additional lookup probes** -- uses structured NuGet paths

For the redistributable zip scenario, only probe #2 is relevant. The DLLs are flat in `net/` and found by flat filename lookup.

### No NuGet cache fallback

Empirical testing confirmed that the resolver does **not** fall back to the NuGet package cache for component resolution. When dependency DLLs were removed from the flat directory but remained in the NuGet cache, `ResolveAssemblyToPath` returned `null`. Resolution requires the DLLs to be present in the component's directory.

## deps.json present vs absent

| Scenario | Behaviour |
|----------|-----------|
| **deps.json present** | Resolver knows exact dependency names and versions. `to_dir_path()` does flat filename lookup in the component directory. Missing assemblies are treated as warnings (`ignore_missing_assemblies=true`), not failures. |
| **deps.json absent** | Resolver falls back to `get_dir_assemblies()` which scans the entire component directory and adds every `.dll` to the TPA list. No version information. Every DLL in `net/` is pre-resolved regardless of ownership. |

With deps.json present, the resolver constructs a precise dependency graph. Without it, the resolver operates as a blind directory scan. The `Load()` whitelist in `OpAmpIsolatedLoadContext` provides the runtime safety net in both cases, but the deps.json makes the contract explicit and testable.

## Transitive dependencies in deps.json

The deps.json also lists transitive dependencies that are **not** loaded into the isolated ALC:

- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `System.Diagnostics.DiagnosticSource`

These are safe because:

1. `Load()` checks `AssembliesToLoad` before consulting the resolver. These assemblies are not in the whitelist, so `Load()` returns `null` and they resolve from the default ALC.
2. During `AssemblyDependencyResolver` construction, the native host pre-resolves paths for all deps.json entries. If a transitive dependency DLL is absent from the flat directory, `ignore_missing_assemblies=true` means it logs a warning rather than failing construction.
3. These assemblies **must** resolve from the default ALC to preserve type identity. If `ILogger` were loaded in both ALCs, interface casts would fail with `InvalidCastException`.

## References

- .NET runtime source: `src/native/corehost/hostpolicy/deps_entry.cpp` (`to_dir_path()` vs `to_package_path()`)
- .NET runtime source: `src/native/corehost/hostpolicy/deps_resolver.cpp` (`probe_deps_entry()`, `resolve_tpa_list()`)
- .NET runtime source: `src/libraries/System.Private.CoreLib/src/System/Runtime/Loader/AssemblyDependencyResolver.cs`
- Microsoft docs: [Create a .NET app with plugins](https://learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support)
- Microsoft docs: [Understanding AssemblyLoadContext](https://learn.microsoft.com/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
