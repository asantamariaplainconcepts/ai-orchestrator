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

namespace AiOrchestrator.Modules.Projects.Features.Identity.UseCases;

/// <summary>
/// UC-002 — an Admin gives, changes and takes away a role on their Project (#13, task 4.1).
/// <para>
/// Granting and changing are one operation, because they are one intent: "this person should be a
/// Member here" has the same meaning whether or not they already held something. A separate change
/// endpoint would only exist so a caller could be told off for guessing wrong.
/// </para>
/// <para>
/// Both refuse to leave the Project with no administrator — the one state nobody can undo from
/// inside the product, since undoing it would itself need the role.
/// </para>
/// </summary>
sealed class AssignProjectRole : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut(
                "/api/projects/{projectId:guid}/roles/{identityId}",
                async (
                    Guid projectId,
                    string identityId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, identityId, request.Role),
                        cancellationToken
                    );

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(AssignProjectRole))
            .WithTags("Identity");

        endpoints
            .MapDelete(
                "/api/projects/{projectId:guid}/roles/{identityId}",
                async (
                    Guid projectId,
                    string identityId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Revoke(projectId, identityId),
                        cancellationToken
                    );

                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .WithName(nameof(Revoke))
            .WithTags("Identity");
    }

    internal sealed record Request(string Role);

    internal sealed record Response(string IdentityId, string Role, DateTimeOffset GrantedAt);

    [Requires(Access.AdminOfProject)]
    internal sealed record Command(Guid ProjectId, string IdentityId, string Role)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    [Requires(Access.AdminOfProject)]
    internal sealed record Revoke(Guid ProjectId, string IdentityId)
        : ICommand<ErrorOr<Success>>,
            IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.IdentityId).NotEmpty().MaximumLength(200);

            // The closed set from the enum, named in the message: DEC-034 fixes the bundles at two,
            // and a caller who guessed a third should be told what the two are.
            RuleFor(command => command.Role)
                .Must(value => Enum.TryParse<ProjectRole>(value, ignoreCase: true, out _))
                .WithMessage(
                    $"Role must be one of: {string.Join(", ", Enum.GetNames<ProjectRole>())}."
                );
        }
    }

    internal sealed class Handler(
        ProjectsDbContext database,
        BootstrapAdministrators administrators,
        TimeProvider clock
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var project = await database.Projects.FirstOrDefaultAsync(
                entity => entity.Id == command.ProjectId,
                cancellationToken
            );

            // Only a caller the decorator already let through reaches this, and on a Project that
            // does not exist that caller is a configured administrator — so naming the absence tells
            // them their id is wrong rather than their permissions.
            if (project is null)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            // Signed in at least once (task 4.1). The refusal is the whole reason the people table
            // exists: an identity nobody has ever seen cannot be given anything that would take
            // effect, and a row that looks granted and does nothing is worse than a no.
            var known = await database.People.AnyAsync(
                person => person.IdentityId == command.IdentityId,
                cancellationToken
            );

            if (!known)
            {
                return ProjectErrors.PersonUnknown();
            }

            var role = Enum.Parse<ProjectRole>(command.Role, ignoreCase: true);

            var existing = await database.ProjectRoles.FirstOrDefaultAsync(
                row => row.ProjectId == command.ProjectId && row.IdentityId == command.IdentityId,
                cancellationToken
            );

            if (existing is not null)
            {
                if (
                    existing.Role == ProjectRole.Admin
                    && role != ProjectRole.Admin
                    && await IsTheOnlyAdministrator(command.ProjectId, cancellationToken)
                )
                {
                    return ProjectErrors.LastAdministrator();
                }

                existing.ChangeTo(role);
                await database.SaveChangesAsync(cancellationToken);

                return new Response(
                    existing.IdentityId,
                    existing.Role.ToString(),
                    existing.GrantedAt
                );
            }

            var granted = ProjectRoleAssignment.Grant(
                command.ProjectId,
                command.IdentityId,
                role,
                clock.GetUtcNow()
            );

            database.ProjectRoles.Add(granted);
            await database.SaveChangesAsync(cancellationToken);

            return new Response(granted.IdentityId, granted.Role.ToString(), granted.GrantedAt);
        }

        /// <summary>
        /// True only when demoting them really would leave nobody. A configured administrator holds
        /// Admin everywhere without a row, so where one exists this is not the last of anything —
        /// and refusing anyway would mean saying "nobody could configure it again", which would be
        /// false.
        /// </summary>
        async Task<bool> IsTheOnlyAdministrator(
            Guid projectId,
            CancellationToken cancellationToken
        ) =>
            administrators.IdentityIds.Count == 0
            && await database.ProjectRoles.CountAsync(
                row => row.ProjectId == projectId && row.Role == ProjectRole.Admin,
                cancellationToken
            ) == 1;
    }

    internal sealed class RevokeHandler(
        ProjectsDbContext database,
        BootstrapAdministrators administrators
    ) : IAppCommandHandler<Revoke, ErrorOr<Success>>
    {
        public async Task<ErrorOr<Success>> Handle(
            Revoke command,
            CancellationToken cancellationToken
        )
        {
            var existing = await database.ProjectRoles.FirstOrDefaultAsync(
                row => row.ProjectId == command.ProjectId && row.IdentityId == command.IdentityId,
                cancellationToken
            );

            if (existing is null)
            {
                return ProjectErrors.RoleNotGranted();
            }

            if (existing.Role == ProjectRole.Admin && administrators.IdentityIds.Count == 0)
            {
                var stored = await database.ProjectRoles.CountAsync(
                    row => row.ProjectId == command.ProjectId && row.Role == ProjectRole.Admin,
                    cancellationToken
                );

                if (stored == 1)
                {
                    return ProjectErrors.LastAdministrator();
                }
            }

            database.ProjectRoles.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);

            return Result.Success;
        }
    }
}
