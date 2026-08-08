using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// The agent runtimes of the machine that executes Runs (#279).
/// </summary>
sealed class GetAgentRuntimes : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runtimes",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetAgentRuntimes))
            .WithTags("Runs");

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query : IQuery<Response>;

    /// <summary>
    /// The agent runtimes of the process that executes Runs (#279). Machine facts — anyone's to
    /// see, no Story named. <paramref name="Hosted"/> false means Runs execute somewhere this
    /// process cannot see, which the panel must not render as "nothing is ready".
    /// </summary>
    internal sealed record Response(
        bool Hosted,
        DateTimeOffset? CheckedAt,
        int RetrySeconds,
        IReadOnlyList<RuntimeView> Runtimes,
        /// <summary>
        /// The machine those runtimes describe, when it is not simply this process — a habitat
        /// that executes agents in sandboxes has preconditions of its own, and a runtime's
        /// readiness means nothing until they are met. Null where the question does not arise.
        /// </summary>
        AgentHostView? Host
    );

    /// <summary>
    /// Where the agents actually run, for a panel that must not imply this process (the
    /// sandboxing change's D6): <paramref name="Where"/> names the machine in words,
    /// <paramref name="Remedy"/> is the action when it is not ready — never a value.
    /// </summary>
    internal sealed record AgentHostView(string Where, bool Ready, string? Remedy);

    /// <summary>
    /// One runtime's readiness with its remedies attached: <paramref name="InstallCommand"/> is
    /// the copyable fix for a missing CLI, pinned where the sentences live (#279 design D3);
    /// <paramref name="CredentialReady"/> is null when no credential is configured — the
    /// switched-off state that runs with the machine's own session, a different sentence from
    /// both "resolves" and "does not".
    /// </summary>
    internal sealed record RuntimeView(
        string Name,
        string Command,
        bool CliReady,
        string InstallCommand,
        string? CredentialSecretName,
        bool? CredentialReady,
        /// <summary>
        /// Why this runtime's session could not be carried to the machine that runs it (#288);
        /// null when the question does not arise. Distinct from a missing secret, because on a
        /// machine you are signed into "the secret is not stored" is the confusing half of the
        /// truth.
        /// </summary>
        string? SessionUnavailableReason,
        /// <summary>The copyable command that starts the way out; null exactly when the reason is.</summary>
        string? SessionUnavailableRemedy
    );

    internal sealed class Handler(IAgentRuntimesMonitor runtimes)
        : IAppQueryHandler<Query, Response>
    {
        public Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var runtimesSnapshot = runtimes.Snapshot();

            return Task.FromResult(
                new Response(
                    runtimesSnapshot.Hosted,
                    runtimesSnapshot.CheckedAt,
                    (int)runtimesSnapshot.ProbeInterval.TotalSeconds,
                    [
                        .. runtimesSnapshot.Runtimes.Select(state => new RuntimeView(
                            state.Name,
                            state.Command,
                            state.CliReady,
                            state.InstallCommand,
                            state.CredentialSecretName,
                            state.CredentialReady,
                            state.SessionUnavailableReason,
                            state.SessionUnavailableRemedy
                        )),
                    ],
                    runtimesSnapshot.Host is { } agentHost
                        ? new AgentHostView(agentHost.Where, agentHost.Ready, agentHost.Remedy)
                        : null
                )
            );
        }
    }
}
