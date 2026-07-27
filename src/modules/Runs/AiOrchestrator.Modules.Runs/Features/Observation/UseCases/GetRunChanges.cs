using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// UC-024 — what the Agent actually changed, read live through the Connector at the Run's
/// linked change (BR-008). A Run with no pull request answers with an explicit absence rather
/// than a 404 pretending the Run itself is missing: the three absences the spec names — no
/// change, no files, a failed read — must stay three facts.
/// </summary>
sealed class GetRunChanges : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/changes",
                async (
                    Guid projectId,
                    Guid runId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new Query(projectId, runId), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(GetRunChanges))
            .WithTags("Runs");

    /// <summary>Null <see cref="Change"/> means no pull request references the Story yet.</summary>
    internal sealed record Response(ChangeView? Change);

    internal sealed record ChangeView(int Number, string Url, IReadOnlyList<FileView> Files);

    internal sealed record FileView(
        string Path,
        string Status,
        int Additions,
        int Deletions,
        string? Patch,
        string? PatchOmittedReason
    );

    internal sealed record Query(Guid ProjectId, Guid RunId) : IQuery<ErrorOr<Response>>;

    internal sealed class Handler(RunsDbContext database, IChangeFileReader changes)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var run = await database
                .Runs.Where(entity =>
                    entity.Id == query.RunId && entity.ProjectId == query.ProjectId
                )
                .Select(entity => new { entity.VendorStoryId })
                .FirstOrDefaultAsync(cancellationToken);

            if (run is null)
            {
                return RunsErrors.RunNotFound(query.RunId);
            }

            var files = await changes.ForStory(
                query.ProjectId,
                run.VendorStoryId,
                cancellationToken
            );

            return files is null
                ? new Response(Change: null)
                : new Response(
                    new ChangeView(
                        files.Number,
                        files.Url,
                        [
                            .. files.Files.Select(file => new FileView(
                                file.Path,
                                file.Status,
                                file.Additions,
                                file.Deletions,
                                file.Patch,
                                file.PatchOmittedReason
                            )),
                        ]
                    )
                );
        }
    }
}
