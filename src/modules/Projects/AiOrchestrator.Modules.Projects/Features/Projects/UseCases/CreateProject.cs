using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
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

    internal sealed record Command(string Name) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
    }

    internal sealed class Handler(ProjectsDbContext database)
        : IAppCommandHandler<Command, ErrorOr<Response>>
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
            await database.SaveChangesAsync(cancellationToken);

            return new Response(project.Id, project.Name);
        }
    }
}
