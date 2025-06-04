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

# Elastic Distribution of OpenTelemetry .NET release notes [edot-dotnet-release-notes]

Review the changes, fixes, and more in each version of Elastic Distribution of OpenTelemetry .NET.

To check for security updates, go to [Security announcements for the Elastic stack](https://discuss.elastic.co/c/announcements/security-announcements/31).

% Release notes include only features, enhancements, and fixes. Add breaking changes, deprecations, and known issues to the applicable release notes sections.

% ## version.next [kibana-X.X.X-release-notes]

% ### Features and enhancements [kibana-X.X.X-features-enhancements]
% *

% ### Fixes [kibana-X.X.X-fixes]
% *

## 1.0.2

### Features and enhancements

- Log after adding AspNetCore trace instrumentation. Improves the trace logging for diagnostic and support purposes.

### Fixes 

- No longer default to IncludeScopes. The upstream SDK isn't spec-compliant regarding not exporting duplicate attributes. As such, when using IncludeScopes in ASP.NET Core, log entries often include a duplicated RequestId attribute. The Elasticsearch exporter component of the collector expects the data to comply with the spec and, for performance reasons, doesn't attempt to de-duplicate, resulting in export errors for the log record. EDOT .NET no longer enables IncludeScopes by default as a partial workaround. This will be re-enabled in a future release, once the risk of data loss is resolved upstream.

## 1.0.1

### Features and enhancements

- Removed invalid prefix from minver command.

## 1.0.0

### Features and enhancements

- Update docs for GA.
- Simplify reflection to work on classic ASP.NET and singlefile publish.
- Ignore warning for unstable dependencies.
- Disable generateapichanges for release.