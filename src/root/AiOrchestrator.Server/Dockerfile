# Build context is the repository root — the SPA lives outside src/ and must be built into
# wwwroot before the host is published, exactly as `pnpm build` does locally.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Node for the SPA. The .NET image has no Node, and the frontend is part of this artifact:
# shipping a host with a stale (or absent) wwwroot is the failure CI already caught once.
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g pnpm@10 \
    && rm -rf /var/lib/apt/lists/*

# Restore layers first, keyed only on manifests, so a source edit does not re-download the world.
COPY src/Directory.Build.props src/Directory.Packages.props src/global.json src/AiOrchestrator.slnx ./src/
COPY src/shared/ ./src/shared/
COPY src/modules/ ./src/modules/
COPY src/root/ ./src/root/
COPY src/tests/ ./src/tests/
RUN dotnet restore src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj

COPY src/frontend/package.json src/frontend/pnpm-lock.yaml ./src/frontend/
RUN cd src/frontend && pnpm install --frozen-lockfile

COPY docs/design-system/ ./docs/design-system/
COPY src/frontend/ ./src/frontend/
RUN cd src/frontend && pnpm build

RUN dotnet publish src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The docker CLIENT only (#246): the pod arrangement starts a container per Run through
# whatever socket the operator mounted, and the launcher speaks through this binary. One static
# file — not the agent CLIs the grill refused to fatten this image with — and inert until the
# operator grants the socket in their own override.
COPY --from=docker.io/docker:29-cli /usr/local/bin/docker /usr/local/bin/docker

# Non-root: the container needs no write access to its own filesystem.
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "AiOrchestrator.Server.dll"]
