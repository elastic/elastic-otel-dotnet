# Examples.AspNetClassicWebApi

A basic ASP.NET (classic, .NET Framework) Web API application showing how to use the Elastic Distribution of OpenTelemetry .NET.

## Building

This project is intentionally **not** part of `Elastic.OpenTelemetry.slnx`.

It uses an old-style (non-SDK) project that targets .NET Framework and the Visual Studio web application targets
(`System.Web`, `Global.asax`, IIS Express). It can only be built on Windows with Visual Studio (or MSBuild with the
ASP.NET workload) installed. Including it in the solution would break `dotnet build` and `./build.sh` on Linux.

To run the example, open `Examples.AspNetClassicWebApi.csproj` directly in Visual Studio.
