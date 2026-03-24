# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Elastic Distribution of OpenTelemetry .NET (EDOT .NET) — an Elastic-sponsored distribution extending the OpenTelemetry SDK for .NET. Provides Elastic-specific processors, opinionated defaults, OTLP exporter, automatic instrumentation assembly scanning, and OpAmp (Open Agent Management Protocol) support for central configuration.

## Always

- Run `dotnet format` before using the build (`build.sh`) commands.
- Favour using the `build.sh` script for building and testing during development.
- Run all tests after code changes to validate work.

## Build Commands

The build system uses F# with Bullseye. Entry points are `build.sh` (Unix) and `build.bat` (Windows).

```bash
# Full build (clean + format check + compile)
./build.sh build

# Compile only (no clean/format)
./build.sh compile

# Run all tests
./build.sh test

# Run specific test suites
./build.sh test --test-suite=unit
./build.sh test --test-suite=integration
./build.sh test --test-suite=build_verification

# Shorthand aliases for test suites
./build.sh unit-test
./build.sh integrate
./build.sh build-verify

# Full release build (build + redistribute zips + package)
./build.sh release

# Skip dirty working copy / format checks
./build.sh release -c

# Format code
./build.sh format

# Run a single target without dependencies
./build.sh <target> -s
```

Underlying dotnet commands for quick iteration:
```bash
dotnet build -c release
dotnet test -c release
dotnet test -c release --filter "FullyQualifiedName~SomeTestClass"
dotnet format
```

**Required SDK**: .NET 10.0.100 (see `global.json`)

## Architecture

### Solution Structure (Elastic.OpenTelemetry.slnx)

**Source projects** (`src/`):

- **Elastic.OpenTelemetry** — Main NuGet package. Extensions for `HostApplicationBuilder`, `TracerProviderBuilder`, `MeterProviderBuilder`, `LoggerProviderBuilder`. Targets netstandard2.0/2.1, net462, net8.0, net9.0.
- **Elastic.OpenTelemetry.Core** — Core implementation (configuration, diagnostics/logging, builder state). **Not a separate NuGet package** — source-linked into the main package. Marked `IsSourceOnlyProject=true`; skips compilation during solution builds because its `.cs` files are compile-included into consuming projects.
- **Elastic.OpenTelemetry.OpAmp** — OpAmp client implementation with ALC isolation on net8.0+. Source-linked for NuGet builds, separate DLL for zip distribution builds. **Not source-only** — produces assemblies for the redistributable (zip) packaging flow.
- **Elastic.OpenTelemetry.OpAmp.Abstractions** — Interfaces for OpAmp version isolation. Source-linked for NuGet builds. **Not source-only** — produces assemblies for the redistributable (zip) packaging flow.
- **Elastic.OpenTelemetry.AutoInstrumentation** — Profiler-based auto-instrumentation plugin. Targets net462, net8.0. Packaged as both NuGet and redistributable zip.

**Key architectural pattern**: Core, OpAmp, and OpAmp.Abstractions are **source-linked** into the main Elastic.OpenTelemetry package (not separate NuGet packages). They exist as separate projects for code organization but compile into one assembly for NuGet distribution. However, only Core is a **source-only project** — OpAmp and OpAmp.Abstractions also produce standalone assemblies for the zip distribution build (`BuildingForZipDistribution=true`) where they are loaded via `AssemblyLoadContext` isolation.

### OpAmp Isolation Strategy

Two packaging strategies with different OpAmp compilation:

- **NuGet package builds**: OpAmp source compiled directly into AutoInstrumentation.dll
- **Zip distribution builds** (`redistribute` target): Separate OpAmp DLLs with `AssemblyLoadContext` isolation on net8.0+ to prevent version conflicts. Controlled by `BuildingForZipDistribution` MSBuild property and `USE_ISOLATED_OPAMP_CLIENT` compiler directive.

### Build System Details

- `build/scripts/Targets.fs` — Build targets and orchestration
- `build/scripts/Packaging.fs` — Complex packaging logic for zip distributions (downloads OpenTelemetry official release, mutates zips to include EDOT plugins)
- `build/scripts/BuildInformation.fs` — Version information
- `build/scripts/CommandLine.fs` — CLI argument definitions

### Test Structure

- `tests/Elastic.OpenTelemetry.Tests/` — Unit tests (net10.0)
- `tests/Elastic.OpenTelemetry.BuildVerification.Tests/` — Package verification tests (net10.0)
- `tests/AutoInstrumentation.IntegrationTests/` — Integration tests (net10.0)
- `test-applications/` — Test web apps (WebApi for .NET Framework, WebApiDotNet8, WebApiDotNet9)

Test framework: xunit. On Linux, tests only run against net10.0 TFM.

## Code Style

- **Indentation**: Tabs for C#/VB; spaces for F#, JSON, YAML, csproj, md (2-space)
- **C#**: Always use `var`. Expression-bodied members preferred. Allman brace style. Braces optional for single-line blocks.
- **Naming**: Private fields `_camelCase`, constants `PascalCase`, locals/params `camelCase`
- **All warnings are errors** (`TreatWarningsAsErrors`)
- **ConfigureAwait**: Enforced by analyzer — all `await` calls need `ConfigureAwait(false)`
- **AOT/Trim analyzers**: IL3050 and IL2026 are errors (except in AOT example project)
- All assemblies are strong-name signed
