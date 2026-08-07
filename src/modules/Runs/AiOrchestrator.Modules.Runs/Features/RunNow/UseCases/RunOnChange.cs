using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Matching;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.RunNow.UseCases;

/// <summary>
/// run-on-a-pr — a Run launched on an open change with an instruction typed on the spot. The
/// launch is the human intent, so there is no approval phase (UC-012's reasoning), and what a
/// change number means is the vendor's answer: URL and head branch are resolved through the seam
/// at launch and never taken from the caller — a caller-supplied branch would point an agent push
/// at an arbitrary ref under the product's credential (design D3).
/// </summary>
sealed class RunOnChange : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/changes/{changeNumber:int}/runs",
                async (
                    Guid projectId,
                    int changeNumber,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, changeNumber, request.Instruction, request.Runtime),
                        cancellationToken
                    );

                    return result.Match(
                        response => Results.Created($"/api/projects/{projectId}/runs", response),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(RunOnChange))
            .WithTags("Runs");

    // Runtime is optional: absent means the same default the Automation form defaults to. The
    // instruction is the whole point and its emptiness is refused by the validator below.
    internal sealed record Request(string Instruction, string? Runtime = null);

    internal sealed record Response(
        Guid Id,
        int ChangeNumber,
        string State,
        bool Dispatched,
        bool WaitingAtCap
    );

    [Requires(RunPermissions.Trigger)]
    internal sealed record Command(
        Guid ProjectId,
        int ChangeNumber,
        string Instruction,
        string? Runtime = null
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // An empty instruction is refused at the edge: a Run of nothing is a spend of
            // something.
            RuleFor(command => command.Instruction).NotEmpty();
            RuleFor(command => command.ChangeNumber).GreaterThan(0);
        }
    }

    internal sealed class Handler(IChangeReader changes, RunCreator creator)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            // The vendor answers what the number means (BR-008, design D3) — one read per
            // launch, a human gesture rather than a poll.
            var open = await changes.Open(command.ProjectId, cancellationToken);
            if (open.Reason is not null)
            {
                return RunsErrors.ChangesUnavailable(open.Reason);
            }

            var change = open.Changes.FirstOrDefault(candidate =>
                candidate.Number == command.ChangeNumber
            );
            if (change is null)
            {
                return RunsErrors.ChangeNotOpen(command.ChangeNumber);
            }

            var outcome = await creator.CreateForChange(
                command.ProjectId,
                change.Number,
                change.Url,
                change.Title,
                change.HeadBranch,
                command.Instruction.Trim(),
                string.IsNullOrWhiteSpace(command.Runtime) ? null : command.Runtime.Trim(),
                cancellationToken
            );

            return outcome switch
            {
                RunCreation.Dispatched dispatched => new Response(
                    dispatched.RunId,
                    change.Number,
                    nameof(RunState.Queued),
                    Dispatched: true,
                    WaitingAtCap: false
                ),
                RunCreation.QueuedAtCap queued => new Response(
                    queued.RunId,
                    change.Number,
                    nameof(RunState.Queued),
                    Dispatched: false,
                    WaitingAtCap: true
                ),
                RunCreation.DispatchFailed failed => new Response(
                    failed.RunId,
                    change.Number,
                    nameof(RunState.Queued),
                    Dispatched: false,
                    WaitingAtCap: false
                ),
                RunCreation.AlreadyActive => RunsErrors.ChangeHasActiveRun(change.Number),
                RunCreation.ProjectArchived => RunsErrors.ProjectArchived(command.ProjectId),
                RunCreation.PreconditionFailed failed => RunsErrors.LocusRefused(failed.Reason),
                _ => Error.Unexpected(
                    "Runs.UnknownOutcome",
                    "Run creation returned an unknown outcome."
                ),
            };
        }
    }
}
