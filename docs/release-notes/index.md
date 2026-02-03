---
navigation_title: EDOT .NET
description: Release notes for Elastic Distribution of OpenTelemetry .NET.
applies_to:
  stack:
  serverless:
    observability:
products:
  - id: cloud-serverless
  - id: observability
  - id: edot-sdk
---

# {{product.edot-dotnet}} release notes [edot-dotnet-release-notes]

Review the changes, fixes, and more in each version of {{product.edot-dotnet}}.

To check for security updates, go to [Security announcements for the Elastic stack](https://discuss.elastic.co/c/announcements/security-announcements/31).

% Release notes include only features, enhancements, and fixes. Add breaking changes, deprecations, and known issues to the applicable release notes sections.

% ## version.next [edot-dotnet-X.X.X-release-notes]

% ### Features and enhancements [edot-dotnet-X.X.X-features-enhancements]
% *

% ### Fixes [edot-dotnet-X.X.X-fixes]
% *

## 1.3.0 [edot-dotnet-1.3.0-release-notes]

:::{note}
This release includes prepratory work for OpAMP configuration and central configuration. These options do not currently have any impact and should not be used.
:::

### Features and enhancements [edot-dotnet-1.3.0-features-enhancements]

- Add initial config options to configure OpAmp client. [#372](https://github.com/elastic/elastic-otel-dotnet/pull/372)
- Update k8s operator to use wolfi-base. [#383](https://github.com/elastic/elastic-otel-dotnet/pull/383)
- Update to latest upstream packages. [#385](https://github.com/elastic/elastic-otel-dotnet/pull/385)

### Fixes [edot-dotnet-1.3.0-fixes]

- Redact and log the OpAmp header option. [#374](https://github.com/elastic/elastic-otel-dotnet/pull/374)

## 1.2.2 [edot-dotnet-1.2.2-release-notes]

### Fixes [edot-dotnet-1.2.2-fixes]

- Fix file logging when configured in `appSettings`. [#370](https://github.com/elastic/elastic-otel-dotnet/pull/370)

## 1.2.1 [edot-dotnet-1.2.1-release-notes]

### Fixes [edot-dotnet-1.2.1-fixes]

- Ensure environment variable resource attributes apply after EDOT defaults. [#368](https://github.com/elastic/elastic-otel-dotnet/pull/368)

## 1.2.0 [edot-dotnet-1.2.0-release-notes]

### Features and enhancements [edot-dotnet-1.2.0-features-enhancements]

- Upgrade to .NET 10 SDK [#339](https://github.com/elastic/elastic-otel-dotnet/pull/339)
- Update upstream OTel package dependencies. 1.14.0 for SDK and contrib, 1.13.0 for instrumentation. [#343](https://github.com/elastic/elastic-otel-dotnet/pull/343)
- Enhance diagnostic logging capabilities. [#348](https://github.com/elastic/elastic-otel-dotnet/pull/348)

### Fixes [edot-dotnet-1.2.0-fixes]

- Fix version numbers in packaging of PowerShell module. [#297](https://github.com/elastic/elastic-otel-dotnet/pull/297)
- Ensure correct execution order for user-provided delegates. [#325](https://github.com/elastic/elastic-otel-dotnet/pull/325)

## 1.1.0 [edot-dotnet-1.1.0-release-notes]

### Features and enhancements [edot-dotnet-1.1.0-features-enhancements]

- Update to 1.12.x upstream packages. [#287](https://github.com/elastic/elastic-otel-dotnet/pull/287)
- Treat `EventLevel.Verbose` as debug log level when subscribing to SDK events for diagnostics. [#288](https://github.com/elastic/elastic-otel-dotnet/pull/288)

## 1.0.2 [edot-dotnet-1.0.2-release-notes]

### Features and enhancements [edot-dotnet-1.0.2-features-enhancements]

- Log after adding AspNetCore trace instrumentation. Improves the trace logging for diagnostic and support purposes. [#262](https://github.com/elastic/elastic-otel-dotnet/pull/262)

### Fixes [edot-dotnet-1.0.2-fixes]

- No longer default to IncludeScopes. The upstream SDK isn't spec-compliant regarding not exporting duplicate attributes. As such, when using IncludeScopes in ASP.NET Core, log entries often include a duplicated RequestId attribute. The {{es}}` exporter component of the collector expects the data to comply with the spec and, for performance reasons, doesn't attempt to de-duplicate, resulting in export errors for the log record. EDOT .NET no longer enables IncludeScopes by default as a partial workaround. This will be re-enabled in a future release, once the risk of data loss is resolved upstream. [#265](https://github.com/elastic/elastic-otel-dotnet/pull/265)

## 1.0.1 [edot-dotnet-1.0.1-release-notes]

### Features and enhancements [edot-dotnet-1.0.1-features-enhancements]

- Removed invalid prefix from minver command. [#261](https://github.com/elastic/elastic-otel-dotnet/pull/261)

## 1.0.0 [edot-dotnet-1.0.0-release-notes]

### Features and enhancements [edot-dotnet-1.0.0-features-enhancements]

- Update docs for GA. [#256](https://github.com/elastic/elastic-otel-dotnet/pull/256)
- Simplify reflection to work on classic ASP.NET and singlefile publish. [#257](https://github.com/elastic/elastic-otel-dotnet/pull/257)
- Ignore warning for unstable dependencies. [#258](https://github.com/elastic/elastic-otel-dotnet/pull/258)
- Disable generateapichanges for release. [#259](https://github.com/elastic/elastic-otel-dotnet/pull/259)