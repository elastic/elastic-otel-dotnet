# Contributing

NOTE: This document is a work in progress.

## Building the code

Ensure you have the following components installed:

- .NET 10 SDK
- To build and run the NativeAoT tests you will require the prequisites listed at https://learn.microsoft.com/en-gb/dotnet/core/deploying/native-aot/?tabs=windows%2Cnet8#prerequisites, specifically the C++ components optionally installed with Visual Studio.

> [!NOTE]
> During release builds, package validation (`nupkg-validator`) is skipped on Windows because of known file permission issues. Package validation still runs on non-Windows environments.