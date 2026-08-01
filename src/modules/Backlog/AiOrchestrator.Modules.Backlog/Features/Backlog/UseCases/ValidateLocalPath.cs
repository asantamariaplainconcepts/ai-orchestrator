using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// #210 — what the host can say about one folder, before an Admin trusts it as a code source.
/// The same inspection the dispatch refusal uses (BR-016), through the same seam, so
/// configuration-time "clean" and dispatch-time "clean" cannot mean different things.
/// <para>
/// It reads the host's filesystem over HTTP, which is why it is gated twice: to the self-host
/// posture (a cloud deployment answers 404 — the surface is absent, not forbidden) and to the
/// project's Admins. It answers about exactly the path it was given and never lists contents.
/// </para>
/// </summary>
sealed class ValidateLocalPath : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/connector/validate-path",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, request.Path),
                        cancellationToken
                    );

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(ValidateLocalPath))
            .WithTags("Backlog");

    internal sealed record Request(string Path);

    /// <summary>Four facts about one path — enough for the UI to name the failing check.</summary>
    internal sealed record Response(
        bool IsDirectory,
        bool IsGitRepository,
        string? Branch,
        bool? IsClean
    );

    [Requires(BacklogPermissions.Configure)]
    internal sealed record Command(Guid ProjectId, string Path)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Path)
                .NotEmpty()
                .MaximumLength(500)
                .Must(System.IO.Path.IsPathFullyQualified)
                .WithMessage("Name an absolute path on the host.");
        }
    }

    internal sealed class Handler(ILocalCodeWorkspace workspace, IConfiguration configuration)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            if (!IdentityHabitat.IsSelfHost(configuration))
            {
                return BacklogErrors.CodeSourceUnavailable();
            }

            var inspection = await workspace.Inspect(command.Path, cancellationToken);

            return new Response(
                inspection.IsDirectory,
                inspection.IsGitRepository,
                inspection.Branch,
                inspection.IsClean
            );
        }
    }
}
