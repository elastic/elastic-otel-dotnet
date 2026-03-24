FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /work
COPY . .
RUN dotnet publish test-applications/OpAmpBootstrapTestApp/OpAmpBootstrapTestApp.csproj \
    -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OpAmpBootstrapTestApp.dll"]
