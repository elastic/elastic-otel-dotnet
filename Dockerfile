# Based on the OpenTelemetry .NET operator image:
# 	https://github.com/open-telemetry/opentelemetry-operator/blob/main/autoinstrumentation/dotnet/Dockerfile
# To build locally you need to call:
#	- ./build.sh redistribute
# This ensures the distribution is locally available under .artifacts/elastic-distribution
#
# The operator's init container only copies /autoinstrumentation into the application pod;
# nothing else from this image, including ENV, reaches the application container. The operator
# doesn't set OTEL_DOTNET_AUTO_PLUGINS, so it must be set in the Instrumentation resource's
# spec.dotnet.env, otherwise the EDOT .NET plugin isn't loaded. Its value must be:
#	Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation
#
# The operator sets CORECLR_PROFILER_PATH to a fixed directory within the copied files:
#	- before 0.157.0: linux-x64/ (glibc) or linux-musl-x64/ (musl)
#	- 0.157.0 and later: linux/ (glibc) or linux-musl/ (musl), which must hold the native
#	  profiler for the node's architecture (open-telemetry/opentelemetry-operator#5350)
# If that path doesn't exist, the CLR profiler isn't loaded. With the default rule-engine
# settings, startup-hook validation logs an error and prevents automatic instrumentation
# from initializing. With OTEL_DOTNET_AUTO_RULE_ENGINE_ENABLED=false, source-based instrumentation
# may still run, but bytecode instrumentation is unavailable. This image therefore provides
# both layouts (see the symlinks in stage 1).

# Stage 1: collect and expand all distro archives (glibc/musl, x64/arm64)
# This stage only unpacks files, so it runs on the build host's platform rather than the target
# platform. That avoids emulating arm64 when the multi-platform image is built on an amd64 runner.
# TARGETARCH is still the architecture of the image being built.
FROM --platform=$BUILDPLATFORM cgr.dev/chainguard/wolfi-base AS downloader

ARG TARGETARCH

WORKDIR /autoinstrumentation

COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-glibc-arm64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-glibc-x64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-musl-arm64.zip" .
COPY ".artifacts/elastic-distribution/elastic-dotnet-instrumentation-linux-musl-x64.zip" .

# The glibc archive for the target architecture provides the shared files (net/, scripts, LICENSE,
# VERSION, additional-deps-*); only the musl native profiler directory is taken from the musl archive.
# The glibc archive must match the target architecture: additional-deps-copy-plan.txt (added in
# opentelemetry-dotnet-instrumentation 1.18.0 for generate-additional-deps.sh) differs per
# architecture, as it writes the store/ under store/x64 or store/arm64.
#
# Each platform image keeps only its own native profilers, under the architecture-agnostic
# linux/ and linux-musl/ directories expected by operator 0.157.0+. The RID-named directories
# (linux-x64, linux-musl-x64, ...) are relative symlinks to them, as in upstream's image, so they
# still resolve after the init container's `cp -r` (which copies symlinks as symlinks).
# On arm64 the legacy linux-x64 and linux-musl-x64 names also point at the arm64 profilers, so
# operators before 0.157.0 load a profiler that matches the node instead of an x64 binary.
RUN case "${TARGETARCH}" in \
      amd64) arch=x64 ;; \
      arm64) arch=arm64 ;; \
      *) echo "unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac &&\
    unzip "elastic-dotnet-instrumentation-linux-glibc-${arch}.zip" &&\
    unzip "elastic-dotnet-instrumentation-linux-musl-${arch}.zip" "linux-musl-${arch}/*" -d . &&\
    rm elastic-dotnet-instrumentation-*.zip &&\
    mv "linux-${arch}" linux &&\
    mv "linux-musl-${arch}" linux-musl &&\
    ln -s linux "linux-${arch}" &&\
    ln -s linux-musl "linux-musl-${arch}" &&\
    if [ "${arch}" = "arm64" ]; then \
      ln -s linux linux-x64 &&\
      ln -s linux-musl linux-musl-x64; \
    fi &&\
    chmod -R go+r .
# Stage 2: runtime image; Wolfi already provides nonroot (uid/gid 65532)
FROM cgr.dev/chainguard/wolfi-base
COPY --chown=65532:65532 --from=downloader /autoinstrumentation /autoinstrumentation
USER 65532:65532
WORKDIR /autoinstrumentation
