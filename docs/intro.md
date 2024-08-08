<!--
Goal of this doc:
Provide all the information a user needs to determine if the product is a good enough fit for their use case to merit further exploration

Assumptions we're comfortable making about the reader:
* They are familiar with Elastic
* They are familiar with OpenTelemetry
-->

# Introduction

> [!WARNING]
> The Elastic Distribution for OpenTelemetry .NET is not yet recommended for production use. Functionality may be changed or removed in future releases. Alpha releases are not subject to the support SLA of official GA features.
>
> We welcome your feedback! You can reach us by [opening a GitHub issue](https://github.com/elastic/elastic-otel-dotnet/issues) or starting a discussion thread on the [Elastic Discuss forum](https://discuss.elastic.co/tags/c/observability/apm/58/dotnet).

<!-- ✅ Intro -->
The Elastic Distribution for OpenTelemetry .NET ("the distro") is a .NET package that provides:

* An easy way to instrument your application with OpenTelemetry.
* Configuration defaults for best usage.

<!-- ✅ What is it? -->
<!-- ✅ Why use it? -->
A _distribution_ is a customized version of an upstream OpenTelemetry repository with some customizations. The Elastic Distribution for OpenTelemetry .NET is an extension of the [OpenTelemetry SDK for .NET](https://opentelemetry.io/docs/zero-code/net/configuration/) and includes the following customizations:

* Uses Elastic-specific processors that ensure optimal compatibility when exporting OpenTelemetry signal data to an Elastic backend like Elastic Observability deployment.
* Preconfigures the collection of tracing and metrics signals, applying some opinionated defaults, such as which sources are collected by default.
* Ensures that the OpenTelemetry protocol (OTLP) exporter is enabled by default.

> [!NOTE]
> For more details about OpenTelemetry distributions in general, visit the [OpenTelemetry documentation](https://opentelemetry.io/docs/concepts/distributions).

<!-- ✅ How to use it? -->
Use the distro to start the OpenTelemetry SDK with your .NET application to automatically capture tracing data, performance metrics, and logs. Traces, metrics, and logs are sent to any OTLP collector you choose.

Start with helpful defaults to begin collecting and exporting OpenTelemetry signals quickly. Then, further refine how you use the distro using extension methods that allow you to fully control the creation of the underlying tracer and metric providers.

After you start sending data to Elastic, use an [Elastic Observability](https://www.elastic.co/guide/en/observability/current/index.html) deployment &mdash; hosted on Elastic Cloud or on-premises &mdash; to monitor your applications, create alerts, and quickly identify root causes of service issues.

<!-- ✅ What they should do next -->
**Ready to try out the distro?** Follow the step-by-step instructions in [Get started](./get-started.md).
