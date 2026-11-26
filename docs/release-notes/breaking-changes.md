---
navigation_title: Breaking changes 
description: Breaking changes for Elastic Distribution of OpenTelemetry .NET.
applies_to:
  stack:
  serverless:
    observability:
products:
  - id: cloud-serverless
  - id: observability
  - id: edot-sdk
---

# Elastic Distribution of OpenTelemetry .NET breaking changes [edot-dotnet-breaking-changes]

Breaking changes can impact your Elastic applications, potentially disrupting normal operations. Before you upgrade, carefully review the Elastic Distribution of OpenTelemetry .NET breaking changes and take the necessary steps to mitigate any issues.

% ## Next version [edot-dotnet-X.X.X-breaking-changes]

% Use the following template to add entries to this document.

% TEMPLATE START
% ::::{dropdown} Title of breaking change 
% Description of the breaking change.
% **Impact**<br> Impact of the breaking change.
% **Action**<br> Steps for mitigating impact.
% View [PR #](PR link).
% ::::
% TEMPLATE END

## 1.8.0 [edot-dotnet-1.8.0-breaking-changes]

::::{dropdown} .NET 8 and .NET 9 targets removed
.NET 8 and .NET 9 have reached end of support and aren't supported by EDOT .NET. In 1.8.0, the `Elastic.OpenTelemetry` NuGet package no longer ships `net8.0` and `net9.0` targets. It now targets .NET 10 and .NET 11, alongside .NET Framework and .NET Standard. The `Elastic.OpenTelemetry.AutoInstrumentation` NuGet package and the auto-instrumentation zip distribution now target .NET 10 or later on modern .NET.

**Impact**<br> Applications on .NET 8 or .NET 9 that upgrade might still run, because the package falls back to its `netstandard2.1` build. That build is unsupported on those runtimes and loses features that depended on the removed targets:

- **Central configuration (OpAMP) stops working.** The OpAMP client is only compiled for .NET Framework and .NET 10 or later. On the `netstandard` build, EDOT .NET logs that OpAMP isn't supported on the platform and doesn't fetch remote configuration.
- **Native AOT and trimming safeguards are lost.** The AOT-compatible and trim-analyzed build, and the runtime guard that skips reflection-based instrumentation scanning when dynamic code isn't available, only apply to .NET 10 or later. Assembly scanning always runs on the `netstandard` build, so Native AOT and trimmed applications can fail or silently lose instrumentation. The compile-time configuration binding generator isn't used either.
- **Built-in .NET 9 instrumentation is no longer used.** EDOT .NET no longer adds the built-in `System.Net.Http` and `System.Runtime` instrumentation on .NET 9. It uses the contrib HTTP and runtime instrumentation packages instead, which might change the telemetry you collect.
- **Auto-instrumentation isn't available.** The auto-instrumentation NuGet package has no .NET 8 or .NET 9 target, so it can't be installed on those runtimes. The zip distribution's EDOT plugin is built for .NET 10 or later.

Other runtime-specific behavior and optimizations might also be lost. These aren't tested or supported, and might break in future releases without notice.

**Action**<br> Upgrade your applications to .NET 10 or later before upgrading to EDOT .NET 1.8.0. If you can't upgrade yet, you can stay on an earlier EDOT .NET version, but this doesn't extend support for .NET 8 or .NET 9, which are end of life. If you rely on central configuration, Native AOT, or auto-instrumentation, don't upgrade EDOT .NET on .NET 8 or .NET 9.

View [PR #476](https://github.com/elastic/elastic-otel-dotnet/pull/476).
::::
