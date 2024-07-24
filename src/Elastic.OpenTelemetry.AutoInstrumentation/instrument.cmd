@echo off
setlocal

:: This script is expected to be used in a build that specified a RuntimeIdentifier (RID)
set BASE_PATH=%~dp0

set OTEL_DOTNET_AUTO_PLUGINS=Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry

call %BASE_PATH%_instrument.cmd %*