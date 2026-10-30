---
navigation_title: Zero-code manual setup
description: Environment variables required to configure EDOT .NET profiler-based zero-code instrumentation manually, including Kubernetes with the OpenTelemetry Operator.
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

# Configure zero-code instrumentation manually

The [installer scripts](/reference/edot-dotnet/setup/zero-code.md) set the environment variables that enable profiler-based zero-code instrumentation for you. If you can't use them, for example when you build container images, configure Windows services or IIS, or deploy with the OpenTelemetry Operator, you must set those variables yourself.

EDOT .NET is a plugin that runs inside the upstream OpenTelemetry .NET automatic instrumentation. If you set up only the upstream variables, the upstream instrumentation runs but the EDOT plugin doesn't load.

:::{important}
You must set `OTEL_DOTNET_AUTO_PLUGINS` for every manual setup. Without it you get upstream OpenTelemetry .NET automatic instrumentation without the Elastic defaults.
:::

## Required for all scenarios

`OTEL_DOTNET_AUTO_PLUGINS`

Loads the EDOT .NET plugin. Set it to exactly:

```text
Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation
```

`OTEL_DOTNET_AUTO_HOME`

The directory where the EDOT .NET distribution is installed, for example `$HOME/.otel-dotnet-auto`.

`DOTNET_STARTUP_HOOKS`

Path to `net/OpenTelemetry.AutoInstrumentation.StartupHook.dll` under `OTEL_DOTNET_AUTO_HOME`. If the variable already has a value, add the path to the existing list.

## Profiler variables by platform

Set the variables for your platform in addition to the ones above. `<HOME>` is the value of `OTEL_DOTNET_AUTO_HOME`.

### Linux and macOS

| Variable | Value |
|---|---|
| `CORECLR_ENABLE_PROFILING` | `1` |
| `CORECLR_PROFILER` | `{918728DD-259F-4A6A-AC2B-B85E1B658318}` |
| `CORECLR_PROFILER_PATH` | `<HOME>/<RID>/OpenTelemetry.AutoInstrumentation.Native.so` on Linux, or `.dylib` on macOS |

Use one of these runtime identifiers (RID) in `CORECLR_PROFILER_PATH`:

| Platform | RID |
|---|---|
| Linux (glibc) | `linux-x64`, `linux-arm64` |
| Linux (musl, for example Alpine) | `linux-musl-x64`, `linux-musl-arm64` |
| macOS | `osx-x64`, `osx-arm64` |

### Windows

| Variable | Value | Applies to |
|---|---|---|
| `CORECLR_ENABLE_PROFILING` | `1` | .NET |
| `CORECLR_PROFILER` | `{918728DD-259F-4A6A-AC2B-B85E1B658318}` | .NET |
| `CORECLR_PROFILER_PATH_64` | `<HOME>\win-x64\OpenTelemetry.AutoInstrumentation.Native.dll` | .NET |
| `CORECLR_PROFILER_PATH_32` | `<HOME>\win-x86\OpenTelemetry.AutoInstrumentation.Native.dll` | .NET |
| `COR_ENABLE_PROFILING` | `1` | .NET Framework |
| `COR_PROFILER` | `{918728DD-259F-4A6A-AC2B-B85E1B658318}` | .NET Framework |
| `COR_PROFILER_PATH_64` | `<HOME>\win-x64\OpenTelemetry.AutoInstrumentation.Native.dll` | .NET Framework |
| `COR_PROFILER_PATH_32` | `<HOME>\win-x86\OpenTelemetry.AutoInstrumentation.Native.dll` | .NET Framework |
| `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` | `OpenTelemetry.AutoInstrumentation.AspNetCoreBootstrapper` | ASP.NET Core |

The `COR_` prefix configures .NET Framework and the `CORECLR_` prefix configures .NET. The PowerShell module sets all of these, together with `OTEL_DOTNET_AUTO_HOME`, `DOTNET_STARTUP_HOOKS` and `OTEL_DOTNET_AUTO_PLUGINS`, for the current session or for a Windows service.

## Kubernetes with the OpenTelemetry Operator

The [OpenTelemetry Operator](https://github.com/open-telemetry/opentelemetry-operator) injects the upstream variables into the application container. It sets `CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_PROFILER_PATH`, `DOTNET_STARTUP_HOOKS`, `DOTNET_ADDITIONAL_DEPS`, `DOTNET_SHARED_STORE` and `OTEL_DOTNET_AUTO_HOME`. It doesn't set `OTEL_DOTNET_AUTO_PLUGINS`.

Add it to the `dotnet` section of your `Instrumentation` resource, next to the EDOT .NET image:

```yaml
apiVersion: opentelemetry.io/v1alpha1
kind: Instrumentation
metadata:
  name: elastic-instrumentation
spec:
  dotnet:
    image: docker.elastic.co/observability/elastic-otel-dotnet:<VERSION>
    env:
      - name: OTEL_DOTNET_AUTO_PLUGINS
        value: "Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation"
```

Replace `<VERSION>` with the [EDOT .NET release](https://github.com/elastic/elastic-otel-dotnet/releases) you want to use. If you deploy with the kube-stack Helm chart, set the same `env` under `instrumentation.dotnet` in your values file.

The operator injects environment variables when it creates a pod, so restart existing workloads after you change the `Instrumentation` resource.

For the full Kubernetes workflow, including annotations and troubleshooting, refer to [Instrument applications on Kubernetes](docs-content://solutions/observability/get-started/opentelemetry/use-cases/kubernetes/instrumenting-applications.md).

## Verify that EDOT .NET is loaded

Check the resource attributes of the exported telemetry:

| Attribute | EDOT .NET plugin loaded | Plugin not loaded |
|---|---|---|
| `telemetry.distro.name` | `elastic` | `opentelemetry-dotnet-instrumentation` |
| `telemetry.distro.version` | The EDOT .NET version | The upstream instrumentation version |

If you see `opentelemetry-dotnet-instrumentation`, check that `OTEL_DOTNET_AUTO_PLUGINS` is set in the application's environment. In Kubernetes, run `kubectl describe pod` and look at the container environment.
