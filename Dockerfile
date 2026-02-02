# Based on the OpenTelemetry .NET operator image:
# 	https://github.com/open-telemetry/opentelemetry-operator/blob/main/autoinstrumentation/dotnet/Dockerfile
# To build locally you need to call:
#	- ./build.sh redistribute
# This ensures the distribution is locally available under .artifacts/elastic-distribution

# Stage 1: collect and expand all distro archives (glibc/musl, x64/arm64)
FROM cgr.dev/chainguard/wolfi-base AS downloader

WORKDIR /autoinstrumentation

COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-glibc-arm64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-glibc-x64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-musl-arm64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-musl-x64.zip" .

RUN unzip elastic-dotnet-instrumentation-linux-glibc-x64.zip &&\
    unzip elastic-dotnet-instrumentation-linux-glibc-arm64.zip "linux-arm64/*" -d .&&\
    unzip elastic-dotnet-instrumentation-linux-musl-x64.zip "linux-musl-x64/*" -d . &&\
    unzip elastic-dotnet-instrumentation-linux-musl-arm64.zip "linux-musl-arm64/*" -d . &&\
    unzip elastic-dotnet-instrumentation-linux-glibc-arm64.zip "store/arm64/*" -d .&&\
    rm elastic-dotnet-instrumentation-*.zip &&\
    chmod -R go+r .

# Stage 2: runtime image; Wolfi already provides nonroot (uid/gid 65532)
FROM cgr.dev/chainguard/wolfi-base
COPY --chown=65532:65532 --from=downloader /autoinstrumentation /autoinstrumentation
USER 65532:65532
WORKDIR /autoinstrumentation