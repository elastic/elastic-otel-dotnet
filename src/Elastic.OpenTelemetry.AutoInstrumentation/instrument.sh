#!/bin/sh

# This script is expected to be used in a build that specified a RuntimeIdentifier (RID)
BASE_PATH="$(cd "$(dirname "$0")" && pwd)"

export OTEL_DOTNET_AUTO_PLUGINS="Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation"

. $BASE_PATH/_instrument.sh

exec "$@"
