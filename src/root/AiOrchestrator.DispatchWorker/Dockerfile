# The dispatch worker's image. aspnet, not runtime: BuildingBlocks framework-references
# Microsoft.AspNetCore.App, so the shared framework is required transitively however little of it
# a queue-draining process uses. The migration image learned this the hard way (#8).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/Directory.Build.props src/Directory.Packages.props src/global.json src/AiOrchestrator.slnx ./src/
COPY src/shared/ ./src/shared/
COPY src/modules/ ./src/modules/
COPY src/root/ ./src/root/
COPY src/tests/ ./src/tests/
RUN dotnet restore src/root/AiOrchestrator.DispatchWorker/AiOrchestrator.DispatchWorker.csproj

RUN dotnet publish src/root/AiOrchestrator.DispatchWorker/AiOrchestrator.DispatchWorker.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "AiOrchestrator.DispatchWorker.dll"]
