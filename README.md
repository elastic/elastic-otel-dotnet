[![Pull Request Validation](https://github.com/elastic/elastic-otel-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/elastic/elastic-otel-dotnet/actions/workflows/ci.yml)

# Elastic Distribution of OpenTelemetry .NET

The Elastic Distribution of OpenTelemetry .NET (EDOT .NET) provides an extension of the [OpenTelemetry SDK for .NET](https://opentelemetry.io/docs/languages/net).

EDOT .NET makes it easier to get started using OpenTelemetry in your .NET applications through strictly OpenTelemetry native means while also providing a smooth 
and rich out-of-the-box experience with [Elastic Observability](https://www.elastic.co/observability).

We welcome your feedback! You can reach us by [opening a GitHub issue](https://github.com/elastic/elastic-otel-dotnet/issues) or starting a discussion thread
on the [Elastic Discuss forum](https://discuss.elastic.co/tags/c/observability/apm/58/dotnet).

> [!NOTE]
> For more details about OpenTelemetry distributions in general, visit the [OpenTelemetry documentation](https://opentelemetry.io/docs/concepts/distributions).

With EDOT .NET, you have access to all the features of the [OpenTelemetry SDK for .NET](https://github.com/open-telemetry/opentelemetry-dotnet) plus:

* Access to SDK enhancements and bug fixes contributed by the Elastic team _before_ the changes are available upstream in OpenTelemetry repositories.
* Elastic-specific processors that ensure optimal compatibility when exporting OpenTelemetry signal data to an Elastic backend like an Elastic Observability deployment.
* Preconfigured collection of tracing and metrics signals, applying [opinionated defaults](https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults.html), 
such as which instrumentation sources are observed by default.
* Ensuring that the OpenTelemetry protocol [(OTLP) exporter](https://opentelemetry.io/docs/specs/otlp) is enabled by default.
* Instrumentation assembly scanning to automatically enable instrumentation from installed contrib NuGet packages.

**Ready to try out the distro?** Follow the step-by-step instructions in [our quickstart guide](https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/index.html).

## Read the docs

Read our complete [EDOT .NET documentation](https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/index.html) for more information.