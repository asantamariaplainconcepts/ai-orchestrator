# The migration step's own image. No SPA, no web server — it applies migrations and exits, and
# its exit code is what gates the rollout (design D6).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/Directory.Build.props src/Directory.Packages.props src/global.json src/AiOrchestrator.slnx ./src/
COPY src/shared/ ./src/shared/
COPY src/modules/ ./src/modules/
COPY src/root/ ./src/root/
COPY src/tests/ ./src/tests/
RUN dotnet restore src/root/AiOrchestrator.MigrationService/AiOrchestrator.MigrationService.csproj

RUN dotnet publish src/root/AiOrchestrator.MigrationService/AiOrchestrator.MigrationService.csproj \
    -c Release -o /app --no-restore

# aspnet, not runtime. This process serves nothing, so the smaller runtime image looks right —
# and fails at startup: BuildingBlocks framework-references Microsoft.AspNetCore.App for the
# endpoint and ProblemDetails helpers, so the shared framework is required transitively however
# little of it the migrator uses. Found by running it, not by reading the csproj.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "AiOrchestrator.MigrationService.dll"]
