using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Connectors;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// #132 — an Admin asks what the stored credential can actually do, whenever they want.
/// <para>
/// A permission granted in the morning can be revoked by lunchtime, and nothing in this product
/// changes when it happens. So the same probe that gates saving is reachable on demand, against
/// the stored credential, with no token re-entered.
/// </para>
/// <para>
/// A GET, and read-only in the sense that matters: it asks the vendor questions and changes
/// nothing, here or there. A failing test leaves the Connector exactly as it was — the Admin asked
/// a question, not for a change (design D4).
/// </para>
/// </summary>
sealed class TestConnector : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/connector/test",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(projectId), cancellationToken);

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(TestConnector))
            .WithTags("Backlog");

    /// <summary>One entry per capability, each with the vendor's reason when it was refused.</summary>
    internal sealed record CapabilityView(string Capability, bool Succeeded, string? Reason);

    internal sealed record Response(bool Satisfied, IReadOnlyList<CapabilityView> Capabilities);

    [Requires(BacklogPermissions.TestConnector)]
    internal sealed record Query(Guid ProjectId) : IQuery<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Handler(ConnectorAccess access)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            // The same resolution every vendor call uses, so "no Connector" and "no secret" read
            // the same here as everywhere else.
            var context = await access.Resolve(query.ProjectId, cancellationToken);
            if (context.IsError)
            {
                return context.Errors;
            }

            var (connector, coordinates, token) = context.Value;

            // The probe that gates saving, called from its second entry point — never a copy of
            // it (design D5). A test that checked something else would reassure people about a
            // check that no longer decides anything.
            var verdict = await connector.VerifyAccess(
                coordinates,
                ConnectorProbe.DocumentPath,
                token,
                cancellationToken
            );

            return new Response(
                verdict.Satisfied,
                [Describe(verdict.Stories), Describe(verdict.Documents)]
            );
        }

        static CapabilityView Describe(CapabilityResult result) =>
            new(result.Name, result.Succeeded, result.Failure?.Description);
    }
}
