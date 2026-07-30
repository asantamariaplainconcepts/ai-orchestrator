using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Projects.UseCases;

/// <summary>
/// Retires a Project (#121). Archiving stops new work — no polling, no matching, no manual Run —
/// and stops nothing else: every Run, log and figure stays readable, because BR-014 makes that
/// record the audit trail rather than clutter, and a product that discarded it to tidy a list
/// would be trading the wrong thing.
/// <para>
/// Typing the name is the only guard (design D4). No rule refuses the archive — "only if it has
/// no Runs" would make every project that was ever used unarchivable — because the risk being
/// guarded against is doing it by accident, and this is exactly proportionate to that.
/// </para>
/// </summary>
sealed class ArchiveProject : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/archive",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, request.ConfirmName),
                        cancellationToken
                    );
                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .WithName(nameof(ArchiveProject))
            .WithTags("Projects");

        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/restore",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    // Restoring needs no ceremony: nothing is lost by restoring something.
                    var result = await sender.Send(new Restore(projectId), cancellationToken);
                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .WithName(nameof(RestoreProject))
            .WithTags("Projects");
    }

    internal sealed record Request(string ConfirmName);

    [Requires(ProjectPermissions.Archive)]
    internal sealed record Command(Guid ProjectId, string ConfirmName)
        : ICommand<ErrorOr<Success>>,
            IScopedToProject;

    [Requires(ProjectPermissions.Archive)]
    internal sealed record Restore(Guid ProjectId) : ICommand<ErrorOr<Success>>, IScopedToProject;

    internal const string RestoreProject = nameof(RestoreProject);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.ConfirmName).NotEmpty();
    }

    internal sealed class Handler(ProjectsDbContext database, TimeProvider clock)
        : IAppCommandHandler<Command, ErrorOr<Success>>
    {
        public async Task<ErrorOr<Success>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var project = await database.Projects.FirstOrDefaultAsync(
                candidate => candidate.Id == command.ProjectId,
                cancellationToken
            );

            if (project is null)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            // Ordinal, not case-insensitive: the confirmation exists to make the act deliberate,
            // and a name typed in the wrong case was probably not read.
            if (!string.Equals(project.Name, command.ConfirmName, StringComparison.Ordinal))
            {
                return ProjectErrors.ArchiveNotConfirmed(project.Name);
            }

            project.Archive(clock.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);
            return Result.Success;
        }
    }

    internal sealed class RestoreHandler(ProjectsDbContext database)
        : IAppCommandHandler<Restore, ErrorOr<Success>>
    {
        public async Task<ErrorOr<Success>> Handle(
            Restore command,
            CancellationToken cancellationToken
        )
        {
            var project = await database.Projects.FirstOrDefaultAsync(
                candidate => candidate.Id == command.ProjectId,
                cancellationToken
            );

            if (project is null)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            project.Restore();
            await database.SaveChangesAsync(cancellationToken);
            return Result.Success;
        }
    }
}
