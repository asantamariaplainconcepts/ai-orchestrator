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
/// The Agent pods of this machine, as a page (design review 5b). What <c>docker ps</c> shows the
/// operator, joined to what the operator actually cares about: which Run each pod is, why a
/// queued one waits, whether docker is ready at all, and the machine's slot count.
/// <para>
/// Cross-project like the inbox, and scoped the same way: the machine's facts (docker health,
/// concurrency) are anyone's to see, but a pod row names a Story, so rows are filtered to the
/// projects the caller can read. Trigger label and runtime come from the Automation catalog at
/// read time — the sighting carries only the Run id, and denormalising the rest onto it would
/// mirror the mirror.
/// </para>
/// </summary>
sealed class GetAgentPods : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pods",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetAgentPods))
            .WithTags("Runs");

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query : IQuery<Response>;

    /// <summary>
    /// <paramref name="Hosted"/> false means pods do not execute in this process — the panel
    /// says so instead of rendering an empty machine, because "no pods here" and "pods live
    /// somewhere this deployment cannot see" are different sentences. <paramref name="ImagePresent"/>
    /// is null while docker itself is unreachable (the missing-image remedy would be the wrong
    /// one to offer). <paramref name="RetrySeconds"/> is the probe's own cadence, so the panel's
    /// "retries every 30s" restates behaviour rather than promising it.
    /// </summary>
    internal sealed record Response(
        bool Hosted,
        bool DockerReady,
        bool? ImagePresent,
        DateTimeOffset? CheckedAt,
        int RetrySeconds,
        int MaxConcurrentPods,
        IReadOnlyList<PodView> Pods,
        RuntimesView Runtimes
    );

    /// <summary>
    /// The agent runtimes of the process that executes Runs (#279), beside the pods because the
    /// operator's question is one: "can this machine run my Automations?". Machine facts like
    /// the pods' own — anyone's to see, no Story named. <paramref name="Hosted"/> false means
    /// Runs execute somewhere this process cannot see (a pods habitat's worker, a queue's job),
    /// which the panel must not render as "nothing is ready".
    /// </summary>
    internal sealed record RuntimesView(
        bool Hosted,
        DateTimeOffset? CheckedAt,
        int RetrySeconds,
        IReadOnlyList<RuntimeView> Runtimes
    );

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
        bool? CredentialReady
    );

    /// <summary>
    /// <paramref name="Executing"/> false is a Run claimed off the outbox but waiting for one of
    /// the machine's slots — still Queued in the database, visible nowhere else.
    /// <paramref name="TriggerLabel"/> and <paramref name="Runtime"/> are null when the
    /// Automation no longer exists; the row still renders, because the pod still runs.
    /// </summary>
    internal sealed record PodView(
        Guid RunId,
        Guid ProjectId,
        string? ProjectName,
        string? VendorStoryId,
        string? TriggerLabel,
        string? Runtime,
        bool Executing,
        DateTimeOffset SightedAt
    );

    internal sealed class Handler(
        IAgentPodsMonitor monitor,
        IAgentRuntimesMonitor runtimes,
        RunsDbContext database,
        IProjectCatalog projects,
        IAutomationCatalog automations,
        IProjectPermissions permissions
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var snapshot = monitor.Snapshot();

            var entries = new List<PodView>(snapshot.Pods.Count);

            if (snapshot.Pods.Count > 0)
            {
                var visible = await permissions.VisibleProjects(cancellationToken);

                var ids = snapshot.Pods.Select(pod => pod.RunId).ToArray();
                var runs = (
                    await database
                        .Runs.Where(run => ids.Contains(run.Id))
                        .ToListAsync(cancellationToken)
                ).ToDictionary(run => run.Id);

                // One name lookup per distinct Project, exactly as the inbox does.
                var names = new Dictionary<Guid, string?>();

                foreach (var sighting in snapshot.Pods)
                {
                    // A sighting with no Run row is the launcher racing a test host or a foreign
                    // database — nothing a person could act on from this panel, so it is omitted
                    // rather than rendered as a row with every field blank.
                    if (!runs.TryGetValue(sighting.RunId, out var run))
                    {
                        continue;
                    }

                    if (visible is not null && !visible.Contains(run.ProjectId))
                    {
                        continue;
                    }

                    if (!names.TryGetValue(run.ProjectId, out var projectName))
                    {
                        projectName = await projects.Name(run.ProjectId, cancellationToken);
                        names[run.ProjectId] = projectName;
                    }

                    var automation = run.AutomationId is { } automationId
                        ? await automations.Detail(run.ProjectId, automationId, cancellationToken)
                        : null;

                    entries.Add(
                        new PodView(
                            run.Id,
                            run.ProjectId,
                            projectName,
                            run.VendorStoryId,
                            automation?.TriggerLabel,
                            automation?.Runtime,
                            sighting.Executing,
                            sighting.SightedAt
                        )
                    );
                }
            }

            var runtimesSnapshot = runtimes.Snapshot();

            return new Response(
                snapshot.Hosted,
                snapshot.DockerReady,
                snapshot.ImagePresent,
                snapshot.CheckedAt,
                (int)snapshot.ProbeInterval.TotalSeconds,
                snapshot.MaxConcurrentPods,
                entries,
                new RuntimesView(
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
                            state.CredentialReady
                        )),
                    ]
                )
            );
        }
    }
}
