ARG OTEL_VERSION=1.7.0
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG TARGETARCH
ARG TARGETVARIANT

ENV _PROJECT="Example.AutoInstrumentation"
ENV _PROJECTPATH="${_PROJECT}/${_PROJECT}.csproj"

RUN apt-get update && apt-get install -y unzip

WORKDIR /work

COPY ["examples/${_PROJECTPATH}", "examples/${_PROJECT}/"]
RUN dotnet restore -a $TARGETARCH "examples/${_PROJECT}"

COPY .git .git
COPY examples/${_PROJECT} examples/${_PROJECT}
WORKDIR "/work/examples/${_PROJECT}"
RUN dotnet publish "${_PROJECT}.csproj" -c Release -a $TARGETARCH --no-restore  -o /app/example


FROM build AS final

COPY ".artifacts/elastic-distribution" /distro/elastic
COPY ".artifacts/otel-distribution" /distro/otel

COPY --from=build /app/example /app/example

RUN OTEL_DOTNET_AUTO_HOME="/app/otel" TMPDIR="/distro/elastic/1.7.0" sh /distro/otel/1.7.0/otel-dotnet-auto-install.sh

ENV OTEL_DOTNET_AUTO_HOME="/app/otel"
ENV OTEL_LOG_LEVEL=debug
ENV OTEL_DOTNET_AUTO_PLUGINS="Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=069ca2728db333c1"
ENTRYPOINT ["sh", "/app/otel/instrument.sh", "dotnet", "/app/example/Example.AutoInstrumentation.dll"]
