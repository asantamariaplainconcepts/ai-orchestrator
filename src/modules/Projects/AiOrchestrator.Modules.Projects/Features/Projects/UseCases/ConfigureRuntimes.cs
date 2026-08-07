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
/// project-runtimes (#244) — the Project's runtime settings: the default a runtime-less
/// Automation resolves to at execution time, and the credential secret <b>names</b> per runtime
/// (BR-010: names stored, values never). Admin-scoped both ways (BR-009): the read carries
/// credential names, which describe the project's billing identity, so it is gated exactly like
/// the write rather than offered to every Member.
/// </summary>
sealed class ConfigureRuntimes : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runtimes",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(projectId), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(GetRuntimeSettings))
            .WithTags("Projects");

        endpoints
            .MapPut(
                "/api/projects/{projectId:guid}/runtimes",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, request.DefaultRuntime, request.CredentialNames),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(ConfigureRuntimes))
            .WithTags("Projects");
    }

    internal const string GetRuntimeSettings = nameof(GetRuntimeSettings);

    /// <summary>Full replace, like the Automation update: the form always shows every field.</summary>
    internal sealed record Request(
        string? DefaultRuntime,
        IReadOnlyDictionary<string, string> CredentialNames
    );

    internal sealed record Response(
        string? DefaultRuntime,
        IReadOnlyDictionary<string, string> CredentialNames
    );

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Query(Guid ProjectId) : IQuery<ErrorOr<Response>>, IScopedToProject;

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(
        Guid ProjectId,
        string? DefaultRuntime,
        IReadOnlyDictionary<string, string> CredentialNames
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // The same closed set the Automation form validates against; absent means the
            // deployment default and is not validated as a name.
            RuleFor(command => command.DefaultRuntime)
                .Must(value => value is null || Enum.TryParse<AgentRuntime>(value, out _))
                .WithMessage(
                    $"Default runtime must be one of: {string.Join(", ", Enum.GetNames<AgentRuntime>())}."
                );

            RuleForEach(command => command.CredentialNames.Keys)
                .Must(key => Enum.TryParse<AgentRuntime>(key, out _))
                .WithMessage(
                    $"Credential keys must each be one of: {string.Join(", ", Enum.GetNames<AgentRuntime>())}."
                );
        }
    }

    internal sealed class QueryHandler(ProjectsDbContext database)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var project = await database
                .Projects.Include(entity => entity.RuntimeCredentials)
                .FirstOrDefaultAsync(entity => entity.Id == query.ProjectId, cancellationToken);

            return project is null
                ? ProjectErrors.NotFound(query.ProjectId)
                : new Response(
                    project.DefaultRuntime,
                    project.RuntimeCredentials.ToDictionary(
                        credential => credential.Runtime,
                        credential => credential.SecretName
                    )
                );
        }
    }

    internal sealed class Handler(ProjectsDbContext database)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var project = await database
                .Projects.Include(entity => entity.RuntimeCredentials)
                .FirstOrDefaultAsync(entity => entity.Id == command.ProjectId, cancellationToken);
            if (project is null)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            project.ConfigureRuntimes(command.DefaultRuntime, command.CredentialNames);
            await database.SaveChangesAsync(cancellationToken);

            return new Response(
                project.DefaultRuntime,
                project.RuntimeCredentials.ToDictionary(
                    credential => credential.Runtime,
                    credential => credential.SecretName
                )
            );
        }
    }
}
