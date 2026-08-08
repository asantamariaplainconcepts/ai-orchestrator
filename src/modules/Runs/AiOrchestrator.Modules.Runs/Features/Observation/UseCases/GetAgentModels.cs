using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// What models a runtime offers, for every surface that lets somebody choose one (#291) — the
/// Automation form and each human launch dialog read the same endpoint, so the two cannot come to
/// disagree about what this machine can run.
/// <para>
/// One endpoint per runtime rather than a map of all of them: asking costs a sandbox where agents
/// are sandboxed (design D2), and a form showing one runtime at a time has no use for the others'
/// answers.
/// </para>
/// <para>
/// Read-only and machine-shaped — like docker's health on the pods panel, what this machine can
/// run is anybody's to see. No Story or project is named in the answer.
/// </para>
/// </summary>
sealed class GetAgentModels : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/agent-runtimes/{runtimeName}/models",
                async (string runtimeName, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(runtimeName), cancellationToken))
            )
            .WithName(nameof(GetAgentModels))
            .WithTags("Runs");

    // Nothing to scope it to: what this machine can run names no project and no Story. The same
    // declaration the pods panel's machine facts would carry if they were asked one at a time.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string RuntimeName) : IQuery<Response>;

    /// <summary>
    /// <paramref name="Source"/> is the whole point of this shape. "Here are the models", "this
    /// runtime's list comes from configuration and none are declared", and "the machine could not
    /// be asked" are three different things to tell somebody, and an empty <paramref name="Models"/>
    /// alone cannot tell them apart (design D6). A surface that collapsed them would say a runtime
    /// has no models when in fact nobody looked.
    /// </summary>
    internal sealed record Response(
        string RuntimeName,
        IReadOnlyList<string> Models,
        string Source
    );

    internal sealed class Handler(IAgentModelCatalog models) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var options = await models.For(query.RuntimeName, cancellationToken);

            return new Response(
                query.RuntimeName,
                options.Models,
                options.Source switch
                {
                    AgentModelSource.Enumerated => "enumerated",
                    AgentModelSource.CouldNotAsk => "couldNotAsk",
                    _ => "declared",
                }
            );
        }
    }
}
