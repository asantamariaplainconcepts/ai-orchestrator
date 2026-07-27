using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Matching;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.RunNow.UseCases;

/// <summary>
/// UC-012 — the human bypass. Detection is the only thing bypassed (BR-013): the Story and
/// Automation are validated through the same Contracts reads matching uses, and creation goes
/// through the same <see cref="RunCreator"/> — then the shared outcomes become answers instead
/// of the handler's silences (design D1/D3).
/// </summary>
sealed class RunNow : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/runs",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, request.VendorStoryId, request.AutomationId),
                        cancellationToken
                    );

                    return result.Match(
                        response =>
                            Results.Created(
                                $"/api/projects/{projectId}/runs?vendorStoryId={response.VendorStoryId}",
                                response
                            ),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(RunNow))
            .WithTags("Runs");

    internal sealed record Request(string VendorStoryId, Guid AutomationId);

    internal sealed record Response(
        Guid Id,
        string VendorStoryId,
        string State,
        bool Dispatched,
        bool WaitingAtCap
    );

    internal sealed record Command(Guid ProjectId, string VendorStoryId, Guid AutomationId)
        : ICommand<ErrorOr<Response>>;

    internal sealed class Handler(
        IStoryReader stories,
        IAutomationCatalog automations,
        RunCreator creator
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var story = await stories.Find(
                command.ProjectId,
                command.VendorStoryId,
                cancellationToken
            );
            if (story is null)
            {
                return RunsErrors.StoryNotFound(command.VendorStoryId);
            }

            // One source of truth for "what can run" (design D2): absent from the enabled
            // catalog covers disabled, deleted and foreign alike.
            var candidates = await automations.EnabledAutomations(
                command.ProjectId,
                cancellationToken
            );
            var automation = candidates.FirstOrDefault(candidate =>
                candidate.AutomationId == command.AutomationId
            );
            if (automation is null)
            {
                return RunsErrors.AutomationNotAvailable(command.AutomationId);
            }

            var outcome = await creator.Create(
                command.ProjectId,
                command.VendorStoryId,
                automation,
                cancellationToken
            );

            return outcome switch
            {
                RunCreation.Dispatched dispatched => new Response(
                    dispatched.RunId,
                    command.VendorStoryId,
                    nameof(RunState.Queued),
                    Dispatched: true,
                    WaitingAtCap: false
                ),
                RunCreation.QueuedAtCap queued => new Response(
                    queued.RunId,
                    command.VendorStoryId,
                    nameof(RunState.Queued),
                    Dispatched: false,
                    WaitingAtCap: true
                ),
                RunCreation.DispatchFailed failed => new Response(
                    failed.RunId,
                    command.VendorStoryId,
                    nameof(RunState.Queued),
                    Dispatched: false,
                    WaitingAtCap: false
                ),
                RunCreation.AlreadyActive => RunsErrors.StoryHasActiveRun(command.VendorStoryId),
                _ => Error.Unexpected(
                    "Runs.UnknownOutcome",
                    "Run creation returned an unknown outcome."
                ),
            };
        }
    }
}
