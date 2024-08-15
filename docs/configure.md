<!--
Goal of this doc:
Provide a complete reference of all available configuration options and where/how they can be set. (Any Elastic-specific configuration options are listed directly. General OpenTelemetry configuration options are linked.)

Assumptions we're comfortable making about the reader:
* They are familiar with Elastic
* They are familiar with OpenTelemetry
-->

# Configure

Configure the Elastic Distribution of OpenTelemetry .NET (EDOT .NET) to send data to Elastic.

<!-- ✅ How users set configuration options -->
## Configuration methods

Configure the OpenTelemetry SDK using the mechanisms listed in the [OpenTelemetry documentation](https://opentelemetry.io/docs/languages/net/automatic/configuration/),
including:

* Setting [environment variables](#environment-variables)
* Using the [`IConfiguration` integration](#iconfiguration-integration)
* [Manually configuring](#manual-configuration) EDOT .NET

<!-- Order of precedence -->
Configuration options set manually in the code take precedence over environment variables, and
environment variables take precedence over configuration options set using the `IConfiguration` system.

### Environment variables

<!-- ✅ What and why -->
EDOT .NET can be configured using environment variables.
This is a cross-platform way to configure EDOT .NET and is especially useful in containerized environments.

<!-- ✅ How -->
Environment variables are read at startup and can be used to configure EDOT .NET.
For details of the various options available and their corresponding environment variable names,
see [Configuration options](#configuration-options)

### `IConfiguration` integration

<!-- ✅ What and why -->
In applications that use the "host" pattern, such as ASP.NET Core and worker service, EDOT .NET
can be configured using the `IConfiguration` integration.

<!-- ✅ How -->
This is done by passing an `IConfiguration` instance to the `AddElasticOpenTelemetry` extension
method on the `IServiceCollection`.

When using an `IHostApplicationBuilder` such as modern ASP.NET Core applications, the current `IConfiguration`
can be accessed via the `Configuration` property on the builder:

```csharp
var builder = WebApplication.CreateBuilder(args);
// Access the current `IConfiguration` instance from the builder.
var currentConfig = builder.Configuration;
```

By default, at this stage, the configuration will be populated from the default configuration sources,
including the `appsettings.json` file(s) and command-line arguments. You may use these sources to define
the configuration for the Elastic Distribution of OpenTelemetry .NET.

<!-- ✅ Example -->
For example, you can define the configuration for the Elastic Distribution of OpenTelemetry .NET in the `appsettings.json` file:

```json
{
  "Elastic": {
    "OpenTelemetry": {
      "FileLogDirectory": "C:\\Logs"
    }
  }
}
```

> [!NOTE]
> This example sets the file log directory to `C:\Logs` which enables diagnostic file logging.

Configuration from the "Elastic:OpenTelemetry" section of the `IConfiguration` instance will be
bound to the `ElasticOpenTelemetryOptions` instance used to configure EDOT .NET.

To learn more about the Microsoft configuration system, see
[Configuration in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration).

### Manual configuration

<!-- ✅ What and why -->
In all other scenarios, you can configure EDOT .NET manually in code.

<!-- ✅ How -->
Create an instance of `ElasticOpenTelemetryBuilderOptions` and pass it to the `ElasticOpenTelemetryBuilder`
constructor or an overload of the `AddElasticOpenTelemetry` extension method on the `IServiceCollection`.

<!-- ✅ Example -->
For example, in traditional console applications, you can configure the
Elastic Distribution of OpenTelemetry .NET like this:

```csharp
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;

var services = new ServiceCollection();

// Create an instance of `ElasticOpenTelemetryBuilderOptions`.
var builderOptions = new ElasticOpenTelemetryBuilderOptions
{
  // Create an instance of `ElasticOpenTelemetryOptions` and configure
  // the file log directory by setting the corresponding property.
	DistroOptions = new ElasticOpenTelemetryOptions
	{
    // This example sets the file log directory to `C:\Logs`
    // which enables diagnostic file logging.
		FileLogDirectory = "C:\\Logs",
	}
};

// Pass the `ElasticOpenTelemetryBuilderOptions` instance to the
// `ElasticOpenTelemetryBuilder` constructor to configure EDOT .NET.
await using var session = new ElasticOpenTelemetryBuilder(builderOptions)
	.WithTracing(b => b.AddSource("MySource"))
	.Build();
```

<!-- ✅ List all available configuration options -->
## Configuration options

Because the Elastic Distribution of OpenTelemetry .NET (EDOT .NET) is an extension of the [OpenTelemetry .NET agent](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation), it supports both:

* General OpenTelemetry SDK configuration options
* Elastic-specific configuration options that are only available when using EDOT .NET

### OpenTelemetry SDK configuration options

EDOT .NET supports all configuration options listed in the [OpenTelemetry General SDK Configuration documentation](https://opentelemetry.io/docs/languages/sdk-configuration/general/).

### Elastic-specific configuration options

EDOT .NET supports the following Elastic-specific options.

#### `FileLogDirectory`

* _Type_: String
* _Default_: `string.Empty`

A string specifying the directory where the Elastic Distribution of OpenTelemetry .NET will write diagnostic log files.
When not provided, no file logging will occur. Each new .NET process will create a new log file in the
specified directory.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_FILE_LOG_DIRECTORY` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:FileLogDirectory` |

#### `FileLogLevel`

* _Type_: String
* _Default_: `Information`

Sets the logging level for EDOT .NET.

Valid options: `Critical`, `Error`, `Warning`, `Information`, `Debug`, `Trace` and `None` (`None` disables the logging).

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_FILE_LOG_LEVEL` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:FileLogLevel` |

#### `SkipOtlpExporter`

* _Type_: Bool
* _Default_: `false`

Allows EDOT .NET to used with its defaults, but without enabling the export of telemetry data to
an OTLP endpoint. This can be useful when you want to test applications without sending telemetry data.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_SKIP_OTLP_EXPORTER` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:SkipOtlpExporter` |

#### `ElasticDefaults`

* _Type_: String
* _Default_: `string.Empty`

A comma-separated list of Elastic defaults to enable. This can be useful when you want to enable
only some of the Elastic Distribution of OpenTelemetry .NET opinionated defaults.

Valid options: `None`, `Traces`, `Metrics`, `Logs`, `All`.

Except for the `None` option, all other options can be combined.

When this setting is not configured or the value is `string.Empty`, all Elastic Distribution of OpenTelemetry .NET defaults will be enabled.

When `None` is specified, no Elastic Distribution of OpenTelemetry .NET defaults will be enabled, and you will need to manually
configure the OpenTelemetry SDK to enable collection of telemetry signals. In this mode, EDOT .NET
does not provide any opinionated defaults, nor register any processors, allowing you to start with the "vanilla"
OpenTelemetry SDK configuration. You may then choose to configure the various providers and register processors
as required.

In all other cases, the Elastic Distribution of OpenTelemetry .NET will enable the specified defaults. For example, to enable only
Elastic defaults only for tracing and metrics, set this value to `Traces,Metrics`.

| Configuration method | Key |
|---|---|
| Environment variable | `ELASTIC_OTEL_DEFAULTS_ENABLED` |
| `IConfiguration` integration | `Elastic:OpenTelemetry:ElasticDefaults` |

<!-- ✅ List auth methods -->
## Authentication methods

When sending data to Elastic, there are two ways you can authenticate: using an APM agent key or using a secret token.

### Use an APM agent key (API key)

<!-- ✅ What and why -->
[APM agent keys](https://www.elastic.co/guide/en/observability/current/apm-api-key.html) are
used to authorize requests to an Elastic Observability endpoint.
APM agent keys are revocable, you can have more than one of them, and
you can add or remove them without restarting APM Server.

<!-- ✅ How do you authenticate using this method? -->
To create and manage APM agent keys in Kibana:

1. Go to **APM Settings**.
1. Select the **Agent Keys** tab.

When using an APM agent key, the `OTEL_EXPORTER_OTLP_HEADERS` is set using a
different auth schema (`ApiKey` rather than `Bearer`). For example:

<!-- ✅ Code example -->
```sh
export OTEL_EXPORTER_OTLP_ENDPOINT=https://my-deployment.apm.us-west1.gcp.cloud.es.io
export OTEL_EXPORTER_OTLP_HEADERS="Authorization=ApiKey TkpXUkx...dVZGQQ=="
```

### Use a secret token

<!-- ✅ What is this -->
<!-- ✅ Why use this -->
[Secret tokens](https://www.elastic.co/guide/en/observability/current/apm-secret-token.html) are used to authorize requests to the APM Server. Both EDOT .NET and APM Server must be configured with the same secret token for the request to be accepted.

<!-- ✅ How do you authenticate using this method? -->
You can find the values of these variables in Kibana's APM tutorial.
In Kibana:

1. Go to **Setup guides**.
1. Select **Observability**.
1. Select **Monitor my application performance**.
1. Scroll down and select the **OpenTelemetry** option.
1. The appropriate values for `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_HEADERS` are shown there.

    ![Elastic Cloud OpenTelemetry configuration](images/elastic-cloud-opentelemetry-configuration.png)

    For example:

    ```sh
    export OTEL_EXPORTER_OTLP_ENDPOINT=https://my-deployment.apm.us-west1.gcp.cloud.es.io
    export OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer P....l"
    ```