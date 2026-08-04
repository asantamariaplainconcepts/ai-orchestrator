# The conversation session's image (#166). Its own, not the dispatch worker's: this one serves HTTP
# and holds a warm checkout for the length of one conversation, while that one drains a queue and
# exits. Sharing an image would mean one entrypoint pretending to be two processes.
#
# The one Dockerfile left standing after #257: it bakes OS packages and global agent CLIs below —
# RUN steps the SDK's container publish cannot express — and it belongs to the Azure path
# (infra/deploy.sh), never to the self-host compose, whose images are all SDK-published.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/Directory.Build.props src/Directory.Packages.props src/global.json src/AiOrchestrator.slnx ./src/
COPY src/shared/ ./src/shared/
COPY src/modules/ ./src/modules/
COPY src/root/ ./src/root/
COPY src/tests/ ./src/tests/
RUN dotnet restore src/root/AiOrchestrator.ConversationSession/AiOrchestrator.ConversationSession.csproj

RUN dotnet publish src/root/AiOrchestrator.ConversationSession/AiOrchestrator.ConversationSession.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# The same agent runtime the dispatch worker carries, pinned to the same versions for the same
# reason (#8's Azurite lesson): a conversation and a Run that answered from different CLI versions
# would disagree about the same repository, and only one of them would be under test.
RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && npm install -g @anthropic-ai/claude-code@2.0.44 opencode-ai@1.18.6 \
    && claude --version \
    && opencode --version

WORKDIR /app
COPY --from=build /app .

USER $APP_UID

# The session host reaches this over HTTP on the pool's target port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiOrchestrator.ConversationSession.dll"]
