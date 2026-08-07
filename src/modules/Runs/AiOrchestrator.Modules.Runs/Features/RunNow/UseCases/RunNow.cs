using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
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
                        new Command(
                            projectId,
                            request.VendorStoryId,
                            request.AutomationId,
                            request.Locus,
                            request.Runtime
                        ),
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

    // Locus is optional (#210): absent means the project's default — Local for a local-folder
    // code source, Pod otherwise. Only Run now offers the choice; matching never does.
    // Runtime optional (#244): the human's choice for this Run only, pre-selected from the
    // resolution in the dialog; absent records the resolution itself.
    internal sealed record Request(
        string VendorStoryId,
        Guid AutomationId,
        string? Locus = null,
        string? Runtime = null
    );

    internal sealed record Response(
        Guid Id,
        string VendorStoryId,
        string State,
        bool Dispatched,
        bool WaitingAtCap
    );

    [Requires(RunPermissions.Trigger)]
    internal sealed record Command(
        Guid ProjectId,
        string VendorStoryId,
        Guid AutomationId,
        string? Locus = null,
        string? Runtime = null
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

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

            // Misspelled must not silently mean the default (the Vendor lesson, #210).
            RunLocus? locus = null;
            if (!string.IsNullOrWhiteSpace(command.Locus))
            {
                if (!Enum.TryParse<RunLocus>(command.Locus, ignoreCase: true, out var parsed))
                {
                    return RunsErrors.UnknownLocus(command.Locus);
                }
                locus = parsed;
            }

            var outcome = await creator.Create(
                command.ProjectId,
                command.VendorStoryId,
                automation,
                cancellationToken,
                locus,
                string.IsNullOrWhiteSpace(command.Runtime) ? null : command.Runtime.Trim()
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
                // The human asked, so the human is told why (#121).
                RunCreation.ProjectArchived => RunsErrors.ProjectArchived(command.ProjectId),
                // BR-016 and the impossible pairings (#210): the sentence decided pre-write is
                // the answer, verbatim — the human is looking at the dialog it belongs in.
                RunCreation.PreconditionFailed failed => RunsErrors.LocusRefused(failed.Reason),
                _ => Error.Unexpected(
                    "Runs.UnknownOutcome",
                    "Run creation returned an unknown outcome."
                ),
            };
        }
    }
}
