using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// #214 — a starter lands in the repository as one bounded git write: a starter-scoped branch and
/// a <b>draft</b> pull request, through the same workspace pipeline Runs publish with. No agent
/// pass is spent, and the default branch is never written — a human merges.
/// <para>
/// The offer still writes nothing; this is the explicit action beside it. Presence is re-checked
/// at click time through the same prompt read a Run performs, so "an existing file always wins"
/// is enforced at the moment it matters rather than holding by construction.
/// </para>
/// </summary>
sealed class InstallStarterPrompt : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/starter-prompts/install",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, request.SaveAs),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(InstallStarterPrompt))
            .WithTags("Automations");

    /// <summary>`SaveAs` identifies the starter: distinct across tiers by the catalogue's design.</summary>
    internal sealed record Request(string SaveAs);

    internal sealed record Response(string Url, string Path, string Branch);

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(Guid ProjectId, string SaveAs)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.SaveAs).NotEmpty().MaximumLength(200);
        }
    }

    internal sealed class Handler(IDocumentReader documents, StarterInstaller installer)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var starter = StarterCatalogue
                .Tiers.SelectMany(tier => tier.Prompts)
                .FirstOrDefault(prompt =>
                    string.Equals(prompt.SaveAs, command.SaveAs, StringComparison.Ordinal)
                );

            if (starter is null)
            {
                return StarterInstallErrors.UnknownStarter(command.SaveAs);
            }

            // Presence at click time, through the same read a Run resolves prompts with — the
            // path this refuses on is the path a Run would read (design D3).
            var presence = await documents.ReadPrompt(
                command.ProjectId,
                starter.SaveAs,
                cancellationToken
            );

            if (presence.ResolvedPath is null)
            {
                // No Connector (or a directory the vendor refused): install has nowhere to write.
                return StarterInstallErrors.NoConnector(
                    presence.Failure ?? "this project has no Connector"
                );
            }

            if (presence.Content is not null)
            {
                // An existing file always wins — refused by name, before any workspace exists.
                return StarterInstallErrors.AlreadyPresent(presence.ResolvedPath);
            }

            // Deterministic and starter-scoped (design D2): one branch per starter at most,
            // named for what it carries rather than for a Run that never happened.
            var branch = $"starter/{StarterInstaller.BranchSlug(starter.SaveAs)}";

            var published = await installer.Install(
                command.ProjectId,
                branch,
                [new StarterInstaller.File(presence.ResolvedPath, starter.Content)],
                $"docs(prompts): install the {starter.SaveAs} starter",
                $"Installs the `{starter.SaveAs}` starter prompt at `{presence.ResolvedPath}` "
                    + "so an Automation can name it. Installed from the portal (#214); "
                    + "review and merge to make it available.",
                cancellationToken
            );

            return published.IsError
                ? published.Errors
                : new Response(published.Value, presence.ResolvedPath, branch);
        }
    }
}

/// <summary>The install path's refusals, named for what the Admin can do about each.</summary>
static class StarterInstallErrors
{
    public static Error UnknownStarter(string saveAs) =>
        Error.NotFound(
            "Starter.Unknown",
            $"No starter saves as '{saveAs}'. The catalogue names what can be installed."
        );

    public static Error AlreadyPresent(string path) =>
        Error.Conflict(
            "Starter.AlreadyPresent",
            $"'{path}' already exists in the repository — an existing file always wins. "
                + "Edit it there, or delete it first if you want the starter's version."
        );

    public static Error NoConnector(string detail) =>
        Error.Validation("Starter.NoConnector", $"Installing needs a Connector: {detail}");
}
