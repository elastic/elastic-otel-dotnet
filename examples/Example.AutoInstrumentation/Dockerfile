FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
RUN apt-get update && apt-get install -y unzip curl strace
#USER $APP_UID
WORKDIR /app
RUN chown app:app .
ARG TARGETPLATFORM
ARG TARGETARCH
ARG TARGETVARIANT
RUN printf "I'm building for TARGETPLATFORM=${TARGETPLATFORM}" \
    && printf ", TARGETARCH=${TARGETARCH}" \
    && printf ", TARGETVARIANT=${TARGETVARIANT} \n" \
    && printf "With uname -s : " && uname -s \
    && printf "and  uname -m : " && uname -m

FROM base AS otel
ARG OTEL_VERSION=1.7.0
# install OpenTelemetry .NET Automatic Instrumentation
#ADD --chown=$APP_UID --chmod=777 https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/releases/download/v${OTEL_VERSION}/otel-dotnet-auto-install.sh otel-dotnet-auto-install.sh
ADD https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/releases/download/v${OTEL_VERSION}/otel-dotnet-auto-install.sh otel-dotnet-auto-install.sh
RUN chmod +x otel-dotnet-auto-install.sh
RUN OTEL_DOTNET_AUTO_HOME="/app/otel" sh otel-dotnet-auto-install.sh


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_example
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY ["examples/Example.AutoInstrumentation/Example.AutoInstrumentation.csproj", "examples/Example.AutoInstrumentation/"]
RUN dotnet restore "examples/Example.AutoInstrumentation/Example.AutoInstrumentation.csproj"
COPY . .
WORKDIR "/src/examples/Example.AutoInstrumentation"
RUN dotnet build "Example.AutoInstrumentation.csproj" -c Release -o /app/build_example

FROM build_example AS publish_example
ARG BUILD_CONFIGURATION
RUN dotnet publish "Example.AutoInstrumentation.csproj" -c Release -o /app/example /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_plugin
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY ["README.md", "."]
COPY ["LICENSE.txt", "."]
COPY ["NOTICE.txt", "."]
COPY ["src/Elastic.OpenTelemetry.AutoInstrumentationPlugin/Elastic.OpenTelemetry.AutoInstrumentationPlugin.csproj", "src/Elastic.OpenTelemetry.AutoInstrumentationPlugin/"]
RUN dotnet restore "src/Elastic.OpenTelemetry.AutoInstrumentationPlugin/Elastic.OpenTelemetry.AutoInstrumentationPlugin.csproj"
COPY . .
WORKDIR "/src/src/Elastic.OpenTelemetry.AutoInstrumentationPlugin"
RUN dotnet build "Elastic.OpenTelemetry.AutoInstrumentationPlugin.csproj" -c release 

FROM otel AS final
WORKDIR /app
COPY --from=publish_example /app/example /app/example
COPY --from=otel /app/otel /app/otel

COPY --from=build_plugin /src/.artifacts /app/src
RUN ls -al /app/src
RUN ls -al /app/src/.artifacts
RUN ls -al /app/src/.artifacts/bin

COPY --from=build_plugin /src/.artifacts/bin/Elastic.OpenTelemetry.AutoInstrumentationPlugin/release_net8.0/Elastic.OpenTelemetry.AutoInstrumentationPlugin.dll /app/otel/net/
COPY --from=build_plugin /src/.artifacts/bin/Elastic.OpenTelemetry/release_net8.0/Elastic.OpenTelemetry.dll /app/otel/net/

ENV CORECLR_ENABLE_PROFILING="1"
ENV CORECLR_PROFILER="{918728DD-259F-4A6A-AC2B-B85E1B658318}"
ENV CORECLR_PROFILER_PATH="/app/otel/linux-${TARGETARCH}/OpenTelemetry.AutoInstrumentation.Native.so"
ENV OTEL_DOTNET_AUTO_PLUGINS="Elastic.OpenTelemetry.AutoInstrumentationPlugin.ElasticAutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentationPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=069ca2728db333c1"
       
#ENV COREHOST_TRACE=1
#ENV COREHOST_TRACEFILE=/app/corehost_verbose_tracing.log

ENV OTEL_TRACES_EXPORTER=none
ENV OTEL_METRICS_EXPORTER=none
ENV OTEL_LOGS_EXPORTER=none
ENV OTEL_SERVICE_NAME=ExampleInstrumentation

ENV OTEL_LOG_LEVEL=info
ENV ELASTIC_OTEL_LOG_LEVEL=trace
ENV ELASTIC_OTEL_LOG_TARGETS=stdout
ENV OTEL_DOTNET_AUTO_LOG_DIRECTORY=/app/logs

ENV OTEL_DOTNET_AUTO_HOME="/app/otel"
ENV DOTNET_ADDITIONAL_DEPS="/app/otel/AdditionalDeps"
ENV DOTNET_SHARED_STORE="/app/otel/store"
ENV DOTNET_STARTUP_HOOKS="/app/otel/net/OpenTelemetry.AutoInstrumentation.StartupHook.dll"

#RUN dotnet /app/example/Example.AutoInstrumentation.dll
#ENTRYPOINT ["ls", "-al", "/app/logs"]
#ENTRYPOINT ["cat", "/app/logs/otel-dotnet-auto-8-dotnet-Native.log"]
#ENTRYPOINT ["cat", "/app/corehost_verbose_tracing.log" ]
#ENTRYPOINT ["ls", "-al", "/app/example"]
ENTRYPOINT ["dotnet", "/app/example/Example.AutoInstrumentation.dll"]
