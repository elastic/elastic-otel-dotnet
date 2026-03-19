# Elastic OpenTelemetry .NET Distribution

## Purpose
The Elastic Distribution of OpenTelemetry .NET (EDOT .NET) — a set of NuGet packages that extend the official OpenTelemetry .NET SDK with Elastic-specific defaults, configurations, and integrations. It provides automatic and manual instrumentation for sending telemetry data to Elastic Observability.

## Tech Stack
- **Language**: C# (.NET 10 SDK, `LangVersion=latest`)
- **Build system**: MSBuild with F# FAKE-based build scripts (`build/scripts/Targets.fs`)
- **Solution file**: `Elastic.OpenTelemetry.slnx` (XML-based slnx format)
- **Package management**: Central Package Management (`Directory.Packages.props`)
- **Versioning**: MinVer (git tag-based semantic versioning)
- **Testing**: xUnit with Microsoft.NET.Test.Sdk
- **CI**: GitHub Actions

## Key Libraries
- OpenTelemetry SDK 1.14.0
- OpenTelemetry.AutoInstrumentation 1.13.0
- OpenTelemetry.OpAmp.Client (alpha) for remote configuration
- Microsoft.Extensions.Hosting / Logging / Http.Resilience
- .NET Aspire (examples)

## Source Projects
- `src/Elastic.OpenTelemetry` — Main distribution package (depends on Core)
- `src/Elastic.OpenTelemetry.Core` — Core library with configuration, OpAmp integration
- `src/Elastic.OpenTelemetry.AutoInstrumentation` — Auto-instrumentation support
- `src/Elastic.OpenTelemetry.OpAmp` — OpAmp client implementation
- `src/Elastic.OpenTelemetry.OpAmp.Abstractions` — OpAmp abstraction interfaces

## Test Projects
- `tests/Elastic.OpenTelemetry.Tests` — Unit tests
- `tests/AutoInstrumentation.IntegrationTests` — Integration tests
- `test-applications/` — Test web applications (WebApi for net10, net8, net9)

## Examples
- ASP.NET Core MVC, Minimal API, Console, Worker Service, WebAPI AOT, Auto-instrumentation
- .NET Aspire AppHost + ServiceDefaults

## Target Frameworks
Multi-targeting: net8.0, net9.0, net10.0 (varies by project)
