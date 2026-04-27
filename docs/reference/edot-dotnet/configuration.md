---
navigation_title: Configuration
description: Configure the Elastic Distribution of OpenTelemetry .NET (EDOT .NET) to send data to Elastic.
applies_to:
  stack:
  serverless:
    observability:
  product:
    edot_dotnet: ga
products:
  - id: cloud-serverless
  - id: observability
  - id: edot-sdk

---

# Configure the EDOT .NET SDK

Configure the {{edot}} .NET (EDOT .NET) to send data to Elastic.

## Configuration methods

Configure the OpenTelemetry SDK using the mechanisms listed in the [OpenTelemetry documentation](https://opentelemetry.io/docs/languages/net/automatic/configuration/),
including:

* Setting [environment variables](#environment-variables).
* Using the [`IConfiguration` integration](#iconfiguration-integration).
* [Manually configuring](#manual-configuration) EDOT .NET.

Configuration options set manually in code take precedence over environment variables, and environment variables take precedence over configuration options set using the `IConfiguration` system.

### Environment variables

You can configure EDOT .NET using environment variables. This is a cross-platform way to configure EDOT .NET and is especially useful in containerized environments.

Environment variables are read at startup and can be used to configure EDOT .NET. For details of the various EDOT-specific options available and their corresponding environment variable names, see [Configuration options](#configuration-options).

All OpenTelemetry environment variables from the contrib SDK may also be used to configure the SDK behavior for features such as resources, samples and exporters.

### IConfiguration integration

In applications that use the [".NET generic host"](https://learn.microsoft.com/dotnet/core/extensions/generic-host), such as [ASP.NET Core](https://learn.microsoft.com/aspnet/core/introduction-to-aspnet-core) and [worker services](https://learn.microsoft.com/dotnet/core/extensions/workers), EDOT .NET can be configured using the `IConfiguration` integration.

When using an `IHostApplicationBuilder` in modern ASP.NET Core applications, the `AddElasticOpenTelemetry` extension method turns on EDOT .NET and configuration from `IHostApplicationBuilder.Configuration` is passed in automatically. For example:

```csharp
var builder = WebApplication.CreateBuilder(args);
// Configuration is automatically bound and can be provided
// via the `appsettings.json` file.
builder.AddElasticOpenTelemetry();
```

By default, at this stage the configuration is populated from the default configuration sources, including the `appsettings.json` file(s) and command-line arguments. You can use these sources to define the configuration for the {{edot}} .NET.

For example, you can define the configuration for the {{edot}} .NET in the `appsettings.json` file:

```json
{
  "Elastic": {
    "OpenTelemetry": {
      "LogDirectory": "C:\\Logs"
    }
  }
}
```

:::{note}
This example sets the file log directory to `C:\Logs` which activates diagnostic file logging.
:::

Configuration parsed from the `Elastic:OpenTelemetry` section of the `IConfiguration` instance is bound to the `ElasticOpenTelemetryOptions` instance used to configure EDOT .NET.

In situations where the application might not depend on the hosting APIs, but uses the dependency injection APIs instead, an `IConfiguration` instance can be passed in manually. This is usually the case with console applications. For example:

```csharp
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>()
    {
        ["Elastic:OpenTelemetry:LogDirectory"] = "C:\\Logs"
    })
    .Build();

var services = new ServiceCollection();
services.AddElasticOpenTelemetry(configuration);
```

To learn more about the Microsoft configuration system, see [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration).

### Manual configuration

In all other scenarios, you can configure EDOT .NET manually in code.

Create an instance of `ElasticOpenTelemetryOptions` and pass it to an overload of the `WithElasticDefaults` extension methods available on the `IHostApplicationBuilder`, the `IServiceCollection` and the specific signal providers such as `TracerProviderBuilder`.

For example, in traditional console applications, you can configure the {{edot}} .NET like this:

```csharp
using OpenTelemetry;
using Elastic.OpenTelemetry;

// Create an instance of `ElasticOpenTelemetryOptions`.
var options = new ElasticOpenTelemetryOptions
{
  // This example sets the file log directory to `C:\Logs`
  // which enables diagnostic file logging.
  FileLogDirectory = "C:\\Logs"
};

// Pass the `ElasticOpenTelemetryOptions` instance to the
// `WithElasticDefaults` extension method for the `IOpenTelemetryBuilder`
//  to configure EDOT .NET.
using var sdk = OpenTelemetrySdk.Create(builder => builder
  .WithElasticDefaults(options));
```

## Configuration options

Because the {{edot}} .NET (EDOT .NET) is an extension of the [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation), it supports both:

* General OpenTelemetry SDK configuration options
* Elastic-specific configuration options that are only available when using EDOT .NET

### OpenTelemetry SDK configuration options

EDOT .NET supports all configuration options listed in the [OpenTelemetry General SDK Configuration documentation](https://opentelemetry.io/docs/languages/sdk-configuration/general/).

### Elastic-specific configuration options

EDOT .NET supports the following Elastic-specific options.

#### `LogDirectory`

* Type: String
* Default: `string.Empty`

A string specifying the output directory where the {{edot}} .NET writes diagnostic log files. When not provided, no file logging occurs. Each new .NET process creates a new log file in the specified directory.

| Configuration method | Key |
|---|---|
| Environment variable | `OTEL_DOTNET_AUTO_LOG_DIRECTORY` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:LogDirectory` |

#### `LogLevel`

* Type: String
* Default: `Information`

Sets the logging level for EDOT .NET. Valid options are `Critical`, `Error`, `Warning`, `Information`, `Debug`, `Trace`, and `None`. `None` disables the logging.

| Configuration method | Key |
|---|---|
| Environment variable | `OTEL_LOG_LEVEL` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:LogLevel` |

#### `LogTargets`

* Type: String
* Default: `Information`

A comma-separated list of targets for log output. When global logging is not configured (a log directory or target is not specified) this defaults to `none`. When the instrumented application is running within a container, this defaults to direct logs to `stdout`. Otherwise defaults to `file`.

Valid options are `file`, `stdout` and `none`. `None` disables the logging.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_LOG_TARGETS` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:LogTargets` |

#### `SkipOtlpExporter`

* Type: Bool
* Default: `false`

Allows EDOT .NET to be used with its defaults, but without enabling the export of telemetry data to
an OTLP endpoint. This can be useful when you want to test applications without sending telemetry data.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_SKIP_OTLP_EXPORTER` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:SkipOtlpExporter` |

#### `SkipInstrumentationAssemblyScanning`

* Type: Bool
* Default: `false`

Allows EDOT .NET to be used without the instrumentation assembly scanning feature turned on. This prevents the automatic registration of instrumentation from referenced [OpenTelemetry contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib) instrumentation packages.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:SkipInstrumentationAssemblyScanning` |

#### `OpAmpEndpoint`

* Type: String
* Default: `string.Empty`

The OpAMP server endpoint used for [central configuration](#central-configuration). When set, EDOT .NET connects to the specified endpoint to receive remote configuration updates. Setting this option also requires a service name. EDOT .NET resolves the service name from `OTEL_SERVICE_NAME` first; if that is not set, it falls back to the `service.name` key within `OTEL_RESOURCE_ATTRIBUTES`. If neither is configured, central configuration is silently disabled.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_OPAMP_ENDPOINT` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:OpAmpEndpoint` |

#### `OpAmpHeaders`

* Type: String
* Default: `string.Empty`

A comma-separated list of HTTP headers in `Key=Value` format to include in requests to the OpAMP server. Use this to pass authentication credentials required by the OpAMP server.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_OPAMP_HEADERS` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:OpAmpHeaders` |

### TLS configuration for OTLP endpoint

To secure the connection to the OTLP endpoint using TLS, you can configure the following environment variables as documented in the [OpenTelemetry OTLP Exporter specification](https://opentelemetry.io/docs/specs/otel/protocol/exporter/):

| Option | Description |
|---|---|
| `OTEL_EXPORTER_OTLP_CERTIFICATE` | Path to a PEM-encoded file containing the trusted certificate(s) to verify the server's TLS credentials. |
| `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` | Path to a PEM-encoded file containing the client certificate for mTLS. |
| `OTEL_EXPORTER_OTLP_CLIENT_KEY` | Path to a PEM-encoded file containing the client's private key for mTLS. |

Signal-specific variants are also supported: `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_CERTIFICATE`, `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_CLIENT_CERTIFICATE`, and `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_CLIENT_KEY`.

:::{note}
HTTPS endpoints are supported for the OpAMP connection used by [central configuration](#central-configuration). Custom certificate verification and mutual TLS (mTLS) for the OpAMP endpoint are not yet available.
:::

## Central configuration

Central configuration allows you to manage EDOT .NET settings remotely without redeploying. When enabled, EDOT .NET connects to an OpAMP-compatible server (the EDOT Collector configured with the Elastic APM Config Extension) and receives configuration at startup from Elastic Observability.

Central configuration takes precedence over all other configuration sources (environment variables and `IConfiguration`). When a remote value is removed, the locally configured value is restored on the next agent startup.

### Activation

To enable central configuration, set the OpAMP server endpoint using one of the following methods:

**Environment variable:**

```sh
export ELASTIC_OTEL_OPAMP_ENDPOINT=http://your-collector:4320/v1/opamp
```

```powershell
$env:ELASTIC_OTEL_OPAMP_ENDPOINT = "http://your-collector:4320/v1/opamp"
```

**`IConfiguration` (for example, `appsettings.json`):**

```json
{
  "Elastic": {
    "OpenTelemetry": {
      "OpAmpEndpoint": "http://your-collector:4320/v1/opamp"
    }
  }
}
```

**Code:**

```csharp
var options = new ElasticOpenTelemetryOptions
{
    OpAmpClientOptions = new OpAmpClientOptions
    {
        Endpoint = "http://your-collector:4320/v1/opamp"
    }
};
```

Central configuration also requires a service name so that EDOT .NET can identify itself to the OpAMP server. EDOT .NET resolves the service name in the following order:

1. `OTEL_SERVICE_NAME` environment variable (or the same key in `IConfiguration`)
2. The `service.name` key within `OTEL_RESOURCE_ATTRIBUTES` (used as a fallback if `OTEL_SERVICE_NAME` is not set)

For example, using `OTEL_SERVICE_NAME`:

```sh
export OTEL_SERVICE_NAME=my-service
```

```powershell
$env:OTEL_SERVICE_NAME = "my-service"
```

Or embedding it in the resource attributes:

```sh
export OTEL_RESOURCE_ATTRIBUTES="service.name=my-service,service.version=1.0.0"
```

```powershell
$env:OTEL_RESOURCE_ATTRIBUTES = "service.name=my-service,service.version=1.0.0"
```

If neither is configured, central configuration is silently disabled.

To pass authentication credentials to the OpAMP server, set the headers option alongside the endpoint:

```sh
export ELASTIC_OTEL_OPAMP_HEADERS="Authorization=Bearer <token>"
```

```powershell
$env:ELASTIC_OTEL_OPAMP_HEADERS = "Authorization=Bearer <token>"
```

### TLS

HTTPS endpoints are supported for the OpAMP connection. Custom certificate verification and mutual TLS (mTLS) for the OpAMP endpoint are not yet available.

### Remotely configurable settings

The following settings can be changed through central configuration without restarting your application:

| Setting | Type |
|---|---|
| Log level | Static (requires restart) |

## Prevent logs export

To prevent logs from being exported, set `OTEL_LOGS_EXPORTER` to `none`. However, application logs might still be gathered and exported by the Collector through the `filelog` receiver.

To prevent application logs from being collected and exported by the Collector, refer to [Exclude paths from logs collection](elastic-agent://reference/edot-collector/config/configure-logs-collection.md#exclude-logs-paths).