using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using DomainProject = AiOrchestrator.Modules.Projects.Domain.Project;

namespace AiOrchestrator.Modules.Projects.Features.Projects.UseCases;

/// <summary>
/// UC-003 — an Admin creates a Project. The exemplar slice: every later use case copies this
/// shape (route + request/response + command + validator + handler, one file, nothing shared
/// outside the module).
/// </summary>
sealed class CreateProject : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.Name), cancellationToken);

                    return result.Match(
                        response => Results.Created($"/api/projects/{response.Id}", response),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(CreateProject))
            .WithTags("Projects");

    internal sealed record Request(string Name);

    internal sealed record Response(Guid Id, string Name);

    // The one operation with no project to hold a role on (#13, design D8). Any signed-in caller may
    // create one, and the handler makes them its Admin — which is not power taken by race, the thing
    // D4 rejects, but power over the one thing they just brought into existence. Without it a
    // deployment's projects could only ever be administered by the configured bootstrap list, and
    // nobody else could get started at all.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Name) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
    }

    internal sealed class Handler(
        ProjectsDbContext database,
        ICurrentPrincipal principal,
        TimeProvider clock
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var nameTaken = await database.Projects.AnyAsync(
                project => project.Name == command.Name,
                cancellationToken
            );

            if (nameTaken)
            {
                return Domain.ProjectErrors.NameAlreadyTaken(command.Name);
            }

            var project = DomainProject.Create(command.Name);
            database.Projects.Add(project);

            // The creator administers what they created (design D8). In the same SaveChanges as the
            // Project, so there is no instant in which a Project exists with nobody able to
            // configure it — a gap a second call could not close, because closing it would itself
            // need the role.
            //
            // Skipped for the sole-occupant habitats: their permissions are composed rather than
            // stored, and a row keyed on "local-owner" would be a grantable person who is not one.
            var creator = principal.Current;
            if (creator.Id is not (Principal.LocalOwnerId or Principal.AnonymousId))
            {
                database.ProjectRoles.Add(
                    Domain.ProjectRoleAssignment.Grant(
                        project.Id,
                        creator.Id,
                        ProjectRole.Admin,
                        clock.GetUtcNow()
                    )
                );
            }

            await database.SaveChangesAsync(cancellationToken);

            return new Response(project.Id, project.Name);
        }
    }
}
