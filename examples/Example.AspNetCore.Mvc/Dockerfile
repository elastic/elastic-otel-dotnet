FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["examples/Example.AspNetCore.Mvc/Example.AspNetCore.Mvc.csproj", "examples/Example.AspNetCore.Mvc/"]
COPY ["src/Elastic.OpenTelemetry/Elastic.OpenTelemetry.csproj", "src/Elastic.OpenTelemetry/"]
COPY ["examples/ServiceDefaults/ServiceDefaults.csproj", "examples/ServiceDefaults/"]
RUN dotnet restore "examples/Example.AspNetCore.Mvc/Example.AspNetCore.Mvc.csproj"
COPY . .
WORKDIR "/src/examples/Example.AspNetCore.Mvc"
RUN dotnet build "Example.AspNetCore.Mvc.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Example.AspNetCore.Mvc.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Example.AspNetCore.Mvc.dll"]
