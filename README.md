[![Pull Request Validation](https://github.com/elastic/elastic-otel-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/elastic/elastic-otel-dotnet/actions/workflows/ci.yml)

# Elastic Distribution for OpenTelemetry .NET

> [!WARNING]
> The Elastic Distribution for OpenTelemetry .NET is not yet recommended for production use. Functionality may be changed or removed in future releases. Alpha releases are not subject to the support SLA of official GA features.
>
> We welcome your feedback! You can reach us by [opening a GitHub issue](https://github.com/elastic/elastic-otel-dotnet/issues) or starting a discussion thread on the [Elastic Discuss forum](https://discuss.elastic.co/tags/c/observability/apm/58/dotnet).

The Elastic Distribution for OpenTelemetry .NET (the distro) provides a zero code change extension of the [OpenTelemetry SDK for .NET](https://opentelemetry.io/docs/languages/net). The distro makes it easier to get started using OpenTelemetry in your .NET applications through strictly OpenTelemetry native means, while also providing a smooth and rich out of the box experience with [Elastic Observability](https://www.elastic.co/observability). It's an explicit goal of this distribution to introduce **no new concepts** in addition to those defined by the wider OpenTelemetry community.

> [!NOTE]
> For more details about OpenTelemetry distributions in general, visit the [OpenTelemetry documentation](https://opentelemetry.io/docs/concepts/distributions).

With the distro you have access to all the features of the OpenTelemetry SDK for .NET plus:

* Access to SDK improvements and bug fixes contributed by the Elastic team _before_ the changes are available upstream in OpenTelemetry repositories.
* Elastic-specific processors that ensure optimal compatibility when exporting OpenTelemetry signal data to an Elastic backend like an Elastic Observability deployment.
* Preconfigured collection of tracing and metrics signals, applying some opinionated defaults, such as which sources are collected by default.
* Ensuring that the OpenTelemetry protocol (OTLP) exporter is enabled by default.

**Ready to try out the distro?** Follow the step-by-step instructions in [Get started](./docs/get-started.md).

## Install

To get started with the Elastic Distribution for OpenTelemetry .NET, you must add the
[`Elastic.OpenTelemetry`](https://www.nuget.org/packages/Elastic.OpenTelemetry)
NuGet package to your project. This can be achieved by adding the package reference to your project file.

```xml
<PackageReference Include="Elastic.OpenTelemetry" Version="<LATEST>" />
```

> [!NOTE]
> Replace the `<LATEST>` placeholder with the latest available package from [NuGet.org](https://www.nuget.org/packages/Elastic.OpenTelemetry).

## Read the docs

* [Get started](./get-started.md)
* [Configuration](./configure.md)

<!-- ## Getting started

As the distribution is a lightweight extension of the OpenTelemetry SDK, you should be broadly
familiar with the OpenTelemetry SDK concepts and instrumenting applications using the Microsoft
diagnostic APIs. If you are not, we recommend you read the
[OpenTelemetry SDK documentation](https://opentelemetry.io/docs/languages/net) first.

It's an explicit goal of this distribution to introduce **no new concepts** as defined by the wider OpenTelemetry community.

### Prerequisites

The current documentation and examples are written with .NET 6 and newer applications in mind.
Before continuing, ensure that you have a supported
[.NET SDK version](https://dotnet.microsoft.com/en-us/download/dotnet) installed locally.

### Installation

```xml
<PackageReference Include="Elastic.OpenTelemetry" Version="<LATEST>" />
```

> **_NOTE:_** Replace the `<LATEST>` placeholder with the latest available package from
[NuGet.org](https://www.nuget.org/packages/Elastic.OpenTelemetry).

After adding the package reference, you can start using the Elastic Distribution for OpenTelemetry .NET
in your application. The distribution includes a transitive dependency on the OpenTelemetry SDK,
so you do not need to add the OpenTelemetry SDK package to your project, although doing so will
cause no harm and may be used to opt into newer SDK versions before the Elastic Distribution for OpenTelemetry .NET
references them.

The Elastic Distribution for OpenTelemetry .NET is designed to be easy to use and integrate into your
applications. This includes applications which have previously used the OpenTelemetry SDK directly.
In situations where the OpenTelemetry SDK is already used, the only required change is
to add the [`Elastic.OpenTelemetry`](https://www.nuget.org/packages/Elastic.OpenTelemetry) NuGet
package to the project. Doing so will automatically switch to the opinionated configuration provided
by the Elastic Distribution for OpenTelemetry .NET.

### ASP.NET Core usage

A common requirement is to instrument ASP.NET Core applications based on the `Microsoft.Extensions.Hosting`
libraries which provide dependency injection via an `IServiceProvider`.

The OpenTelemetry SDK and the Elastic Distribution for OpenTelemetry .NET provide extension methods to enable observability
features in your application by adding a few lines of code.

In this section, we'll focus on instrumenting an ASP.NET Core minimal API application using the Elastic
OpenTelemetry distribution. Similar steps can also be used to instrument other ASP.NET Core workloads
and other host-based applications such as [worker services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers).

> **_NOTE:_** These examples assume the use of the top-level statements feature introduced in C# 9.0 and the
default choice for applications created using the latest templates.

To take advantage of the OpenTelemetry SDK instrumentation for ASP.NET Core, add the following
NuGet package to your project:

```
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="<LATEST>" />
```
> **_NOTE:_** Replace the `<LATEST>` placeholder with the latest available package from [NuGet.org](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore).

This package includes instrumentation to collect traces for requests handled by ASP.NET Core endpoints.

> **_NOTE:_** The ASP.NET Core instrumentation is not included by default in the Elastic Distribution for OpenTelemetry .NET.
As with all optional instrumentation libraries, you can choose to include them in your application by
adding a suitable package reference.

Inside the `Program.cs` file of the ASP.NET Core application, add the following two using directives:

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;
```

The OpenTelemetry SDK provides extension methods on the `IServiceCollection` to support enabling the
providers and configuring the SDK. The Elastic Distribution for OpenTelemetry .NET overrides the default SDK registration,
adding several opinionated defaults.

In the minimal API template, the `WebApplicationBuilder` exposes a `Services` property that can be used
to register services with the dependency injection container. To enable tracing and metrics collection,
ensure that the OpenTelemetry SDK is registered.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddHttpClient() <1>
	.AddOpenTelemetry() <2>
		.WithTracing(t => t.AddAspNetCoreInstrumentation()); <3>
```
<1> The `AddHttpClient` method registers the `IHttpClientFactory` service with the dependency
injection container. This is NOT required to enable OpenTelemetry, but the example endpoint will use it to
send an HTTP request.

<2> The `AddOpenTelemetry` method registers the OpenTelemetry SDK with the dependency injection
container. When available, the Elastic Distribution for OpenTelemetry .NET will override this to add opinionated defaults.

<3> Configure tracing to instrument requests handled by ASP.NET Core.

With these limited changes to the `Program.cs` file, the application is now configured to use the
OpenTelemetry SDK and the Elastic Distribution for OpenTelemetry .NET to collect traces and metrics, which are exported via
OTLP.

To demonstrate the tracing capabilities, add a simple endpoint to the application:

```csharp
app.MapGet("/", async (IHttpClientFactory httpClientFactory) =>
{
	using var client = httpClientFactory.CreateClient();

	await Task.Delay(100);
	var response = await client.GetAsync("http://elastic.co"); <1>
	await Task.Delay(50);

	return response.StatusCode == System.Net.HttpStatusCode.OK ? Results.Ok() : Results.StatusCode(500);
});
```
<1> Using this URL will require two redirects, allowing us to see multiple spans in the trace.

The Elastic Distribution for OpenTelemetry .NET will automatically enable the exporting of signals via the OTLP exporter. This
exporter requires that endpoint(s) are configured. A common mechanism for configuring endpoints is
via environment variables.

This demo uses an Elastic Cloud deployment as the destination for our observability data. From Kibana
running in Elastic Cloud, navigate to the observability set up guides. Select the OpenTelemetry option
to view the configuration details that should be supplied to the application.

![Elastic Cloud OpenTelemetry configuration](https://raw.githubusercontent.com/elastic/elastic-otel-dotnet/main/docs/images/elastic-cloud-opentelemetry-configuration.png)

Configure environment variables for the application either in `launchSettings.json` or in the environment
where the application is running.

Once configured, run the application and make a request to the root endpoint. A trace will be generated
and exported to the OTLP endpoint.

To view the traces, you can use the Elastic APM UI.

![Minimal API request trace sample in the Elastic APM UI](https://raw.githubusercontent.com/elastic/elastic-otel-dotnet/main/docs/images/trace-sample-minimal-api.png)

### Microsoft.Extensions.Hosting usage

For console applications, services, etc that are written against a builder that exposes an `IServiceCollection`
you can install this package:

```xml
<PackageReference Include="Elastic.OpenTelemetry" Version="<LATEST>" />
```
> **_NOTE:_** Replace the `<LATEST>` placeholder with the latest available package from
[NuGet.org](https://www.nuget.org/packages/Elastic.OpenTelemetry).

Ensure you call `AddOpenTelemetry` to enable OpenTelemetry just as you would when using OpenTelemetry directly.
Our package intercepts this call to set up our defaults, but can be further build upon as per usual:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
	.ConfigureResource(r => r.AddService(serviceName: "MyService"))
	.WithTracing(t => t.AddSource(Worker.ActivitySourceName).AddConsoleExporter())
	.WithMetrics(m => m.AddMeter(Worker.MeterName).AddConsoleExporter());
```

### Manual Instrumentation usage

In environments where an `IServiceCollection` is unavailable you may manually start instrumenting by creating
an instance of `ElasticOpenTelemetryBuilder`.

```csharp
await using var session = new ElasticOpenTelemetryBuilder()
    .WithTracing(b => b.AddSource(ActivitySourceName))
    .Build();
```

This will setup instrumentation for as long as `session` is not disposed. We would generally expect the `session`
to live for the life of the application.

`ElasticOpenTelemetryBuilder` is an implementation of [`IOpenTelemetryBuilder`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/70657395b82ba00b8a1e848e8832b77dff94b6d2/src/OpenTelemetry.Api.ProviderBuilderExtensions/IOpenTelemetryBuilder.cs#L12).

This is important to know because any instrumentation configuration is automatically exposed by the base
OpenTelemetry package as extension methods on `IOpenTelemetryBuilder`. You will not lose functionality by
using our builder. -->
