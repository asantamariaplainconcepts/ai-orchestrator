using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// This machine's own sandboxes (#311) — the read behind the surface that is not keyed to a Run.
/// <para>
/// <b>Not <c>IScopedToProject</c>, and that is the whole design question.</b> Every other read here names
/// a project and lets the pipeline's decorator ask for a role on it. A machine's sandboxes belong to the
/// machine: the sandbox an earlier process abandoned resolves to no Run and therefore to no project, so
/// there is no project to scope to. <see cref="MachineSandboxAccess"/> is where that widening is
/// reasoned about; this use case only orders the questions.
/// </para>
/// <para>
/// <b>The habitat answers before the caller does.</b> A deployment hosts no terminal (ADR-0021), and
/// saying so must never read as a permission somebody could ask to be granted — so <c>Hosted</c> is
/// reported as a fact and the permission is not evaluated when it is false.
/// </para>
/// </summary>
sealed class ListMachineSandboxes : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runs/sandboxes",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListMachineSandboxes))
            .WithTags("Runs");

    /// <summary>
    /// No <c>[Requires]</c> and no <c>IScopedToProject</c>: the permission this needs is not held on a
    /// project, so the decorator has nothing to check it against. The handler asks instead, which is the
    /// same exception <c>RunTerminalHub</c> already is — and it is recorded in
    /// <c>ProjectRoles_Should_Constraint</c> rather than left for a reader to notice.
    /// </summary>
    internal sealed record Query : IQuery<Response>;

    /// <summary>
    /// <paramref name="Hosted"/> is the habitat's answer and <paramref name="Permitted"/> the caller's,
    /// kept apart because each has its own sentence and its own remedy — asking for access does not help
    /// a habitat that hosts nothing. <paramref name="Sandboxes"/> is empty unless both are true.
    /// </summary>
    internal sealed record Response(
        bool Hosted,
        bool Permitted,
        IReadOnlyList<SandboxView> Sandboxes
    );

    /// <summary>
    /// <paramref name="Status"/> is the runtime's own word, carried rather than interpreted: entering a
    /// stopped sandbox starts it, and the surface has to be able to say so.
    /// </summary>
    internal sealed record SandboxView(string Name, string Status, Guid? RunId, string? Workspace);

    internal sealed class Handler(
        IRunTerminalHost terminals,
        IProjectPermissions permissions,
        IOptions<PermissionGrants> grants
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            if (!terminals.Hosted)
            {
                // Answered without evaluating any permission, so that a deployment cannot reach the
                // habitat-scoped reading by any path.
                return new Response(Hosted: false, Permitted: false, Sandboxes: []);
            }

            var permitted = await MachineSandboxAccess.MayAttachSomewhere(
                permissions,
                grants.Value,
                cancellationToken
            );

            if (!permitted)
            {
                // The list is withheld, not merely the terminal: what sandboxes exist on the machine is
                // itself the thing `run.attach` guards here.
                return new Response(Hosted: true, Permitted: false, Sandboxes: []);
            }

            var sandboxes = await terminals.List(cancellationToken);

            return new Response(
                Hosted: true,
                Permitted: true,
                Sandboxes:
                [
                    .. sandboxes.Select(sandbox => new SandboxView(
                        sandbox.Name,
                        sandbox.Status,
                        sandbox.RunId,
                        sandbox.Workspace
                    )),
                ]
            );
        }
    }
}
