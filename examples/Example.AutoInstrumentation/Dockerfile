ARG OTEL_VERSION=1.7.0
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
ARG TARGETPLATFORM
ARG TARGETARCH
ARG TARGETVARIANT
RUN apt-get update && apt-get install -y unzip curl strace
# Would love to run as non root but TestContainers does not utilize buildkit like `docker build` does OOTB
#USER $APP_UID
WORKDIR /app
RUN chown app:app .

FROM base AS otel
ARG OTEL_VERSION
# install OpenTelemetry .NET Automatic Instrumentation
# the following commented line does not work from TestContainers because it does not utilize buildkit which `docker build` does OOTB
#ADD --chown=$APP_UID --chmod=777 https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/releases/download/v${OTEL_VERSION}/otel-dotnet-auto-install.sh otel-dotnet-auto-install.sh
ADD https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/releases/download/v${OTEL_VERSION}/otel-dotnet-auto-install.sh otel-dotnet-auto-install.sh
RUN chmod +x otel-dotnet-auto-install.sh
RUN OTEL_DOTNET_AUTO_HOME="/app/otel" sh otel-dotnet-auto-install.sh


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_example
ENV _PROJECT="Example.AutoInstrumentation"
ENV _PROJECTPATH="${_PROJECT}/${_PROJECT}.csproj"
WORKDIR /work
COPY ["examples/${_PROJECTPATH}", "examples/${_PROJECT}/"]
RUN dotnet restore "examples/${_PROJECT}"
COPY .git .git
COPY examples/${_PROJECT} examples/${_PROJECT}
WORKDIR "/work/examples/${_PROJECT}"
RUN dotnet build "${_PROJECT}.csproj" -c Release -o /app/build_example

FROM build_example AS publish_example
RUN dotnet publish "Example.AutoInstrumentation.csproj" -c Release -o /app/example /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_distro
ENV _PROJECT="Elastic.OpenTelemetry"
ENV _PROJECTPATH="${_PROJECT}/${_PROJECT}.csproj"
WORKDIR /work
COPY ["src/${_PROJECTPATH}", "src/${_PROJECT}/"]
RUN dotnet restore "src/${_PROJECTPATH}"
COPY . .
WORKDIR "/work/src/${_PROJECT}"
RUN dotnet build "${_PROJECT}.csproj" -c Release 

FROM otel AS final
ARG TARGETPLATFORM
ARG TARGETARCH
ARG TARGETVARIANT
WORKDIR /app
COPY --from=publish_example /app/example /app/example
COPY --from=build_distro /work/.artifacts/bin/Elastic.OpenTelemetry/release_netstandard2.1/Elastic.OpenTelemetry.dll /app/otel/net/
COPY --from=build_distro /work/.artifacts/bin/Elastic.OpenTelemetry/release_netstandard2.1/Elastic.OpenTelemetry.pdb /app/otel/net/

ENV CORECLR_ENABLE_PROFILING="1"
ENV CORECLR_PROFILER="{918728DD-259F-4A6A-AC2B-B85E1B658318}"
ENV CORECLR_PROFILER_PATH="/app/otel/linux-${TARGETARCH}/OpenTelemetry.AutoInstrumentation.Native.so"
ENV OTEL_DOTNET_AUTO_PLUGINS="Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=069ca2728db333c1"
       
ENV OTEL_TRACES_EXPORTER=none
ENV OTEL_METRICS_EXPORTER=none
ENV OTEL_LOGS_EXPORTER=none
ENV OTEL_SERVICE_NAME=ExampleInstrumentation

ENV OTEL_LOG_LEVEL=debug
ENV OTEL_DOTNET_AUTO_LOG_DIRECTORY=/app/logs
ENV ELASTIC_OTEL_LOG_TARGETS=stdout

ENV OTEL_DOTNET_AUTO_HOME="/app/otel"
ENV DOTNET_ADDITIONAL_DEPS="/app/otel/AdditionalDeps"
ENV DOTNET_SHARED_STORE="/app/otel/store"
ENV DOTNET_STARTUP_HOOKS="/app/otel/net/OpenTelemetry.AutoInstrumentation.StartupHook.dll"

ENV OTEL_DOTNET_AUTO_FAIL_FAST_ENABLED=true

ENTRYPOINT ["dotnet", "/app/example/Example.AutoInstrumentation.dll"]
