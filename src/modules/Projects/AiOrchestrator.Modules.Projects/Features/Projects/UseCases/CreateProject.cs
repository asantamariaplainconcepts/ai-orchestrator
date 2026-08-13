using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
                    var result = await sender.Send(
                        new Command(request.Name, request.Folder),
                        cancellationToken
                    );

                    return result.Match(
                        response => Results.Created($"/api/projects/{response.Id}", response),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(CreateProject))
            .WithTags("Projects");

    /// <summary>
    /// <paramref name="Folder"/> is the self-host shortcut (#347): an absolute path on this machine
    /// whose `origin` names the vendor and the coordinates, so an Admin does not retype what the
    /// repository already knows. Absent everywhere else, and refused rather than ignored in a
    /// deployment that cannot honour it.
    /// </summary>
    internal sealed record Request(string Name, string? Folder = null);

    /// <summary>
    /// <paramref name="Connector"/> says what the folder produced, so the portal can show the
    /// derived coordinates or name which check failed — a folder that answers nothing still creates
    /// the Project, and the Admin types the coordinates on the Connector form.
    /// </summary>
    internal sealed record Response(Guid Id, string Name, FolderOutcome? Connector = null);

    /// <summary>
    /// Either the coordinates a folder yielded, or the one check that stopped it. Never both, and
    /// never a generic failure: the four checks have four different fixes (#347).
    /// </summary>
    internal sealed record FolderOutcome(
        bool Configured,
        string? Vendor,
        string? Owner,
        string? Repository,
        string? CodeRepository,
        string? FailedCheck
    );

    // The one operation with no project to hold a role on (#13, design D8). Any signed-in caller may
    // create one, and the handler makes them its Admin — which is not power taken by race, the thing
    // D4 rejects, but power over the one thing they just brought into existence. Without it a
    // deployment's projects could only ever be administered by the configured bootstrap list, and
    // nobody else could get started at all.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Name, string? Folder = null)
        : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);

            // Absolute, checked here rather than in the handler for the same reason the Connector's
            // own path is: a relative path would resolve against the server's working directory,
            // which is not a folder the Admin was thinking of.
            RuleFor(command => command.Folder!)
                .MaximumLength(500)
                .Must(Path.IsPathRooted)
                .WithMessage("The folder must be an absolute path on this machine.")
                .When(command => !string.IsNullOrWhiteSpace(command.Folder));
        }
    }

    internal sealed class Handler(
        ProjectsDbContext database,
        ICurrentPrincipal principal,
        Features.Identity.KnownPeople people,
        TimeProvider clock,
        ILocalCodeWorkspace folders,
        IConnectorWriter connectors,
        IConfiguration configuration
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var named = !string.IsNullOrWhiteSpace(command.Folder);

            // Refused, never ignored: a deployment has no folder to name, and silently dropping it
            // would let a caller believe it configured something (#347, DEC-049).
            if (named && !IdentityHabitat.IsSelfHost(configuration))
            {
                return Domain.ProjectErrors.FolderNotAvailableHere();
            }

            // Everything a person can get wrong happens BEFORE the first write (design D4): the
            // folder is inspected and its remote parsed here, so a bad folder never leaves a
            // Project behind to clean up.
            FolderOutcome? folder = null;
            RemoteCoordinates? derived = null;

            if (named)
            {
                var inspection = await folders.Inspect(command.Folder!, cancellationToken);

                // Four checks, in the order a person would make them, each naming itself. Written
                // as statements rather than a switch expression on purpose: the parse has to hand
                // back coordinates, and a match arm that assigns through `out` hides that the
                // fourth check is doing two things.
                if (!inspection.IsDirectory)
                {
                    folder = Failed("notADirectory");
                }
                else if (!inspection.IsGitRepository)
                {
                    folder = Failed("notAGitRepository");
                }
                else if (inspection.OriginUrl is null)
                {
                    folder = Failed("noOrigin");
                }
                else if (!GitRemoteCoordinates.TryParse(inspection.OriginUrl, out derived))
                {
                    folder = Failed("unknownVendor");
                }
                else
                {
                    folder = new FolderOutcome(
                        Configured: true,
                        derived.Vendor,
                        derived.Owner,
                        derived.Repository,
                        derived.CodeRepository,
                        FailedCheck: null
                    );
                }
            }

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

                // And recorded as somebody this deployment has met, in the same write. The
                // invariant is that a role-holder is always known: without this the creator held
                // Admin while the grant surface refused to manage them — "that person has not
                // signed in" about the person who just created the project.
                await people.Note(creator, cancellationToken);
            }

            await database.SaveChangesAsync(cancellationToken);

            if (derived is not null)
            {
                var configured = await connectors.CreateFromLocalFolder(
                    project.Id,
                    new LocalFolderConnector(
                        derived.Vendor,
                        derived.Owner,
                        derived.Repository,
                        derived.CodeRepository,
                        command.Folder!.Trim()
                    ),
                    cancellationToken
                );

                if (configured.IsError)
                {
                    // Compensate rather than leave a Project with no Connector — the state this
                    // capability exists to abolish. Safe precisely because this handler created it
                    // moments ago: nothing else can hold a reference to a Project that has not been
                    // returned to anybody yet. The two modules own different schemas, so one
                    // transaction cannot span them (design D4).
                    database.Projects.Remove(project);
                    await database.SaveChangesAsync(cancellationToken);

                    return configured.Errors;
                }
            }

            return new Response(project.Id, project.Name, folder);
        }

        /// <summary>
        /// One named check, so a folder that answers nothing says <b>which</b> of the four it was —
        /// the four have four different fixes, and a generic failure would send the Admin looking at
        /// the wrong one. The Project is still created; only the coordinates are left to type.
        /// </summary>
        static FolderOutcome Failed(string check) =>
            new(
                Configured: false,
                Vendor: null,
                Owner: null,
                Repository: null,
                CodeRepository: null,
                FailedCheck: check
            );
    }
}
