using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-004 — an Admin points a Project at a repository.
/// <para>
/// The credential is verified against the live vendor <b>before</b> anything is stored, so a
/// Connector that exists is one that works. That deliberately makes this endpoint depend on an
/// external service: the alternative is a broken Connector surfacing much later as an empty
/// backlog, with no way to tell it from a repository that simply has no Stories.
/// </para>
/// </summary>
sealed class ConfigureConnector : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/projects/{projectId:guid}/connector",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new Command(
                        projectId,
                        request.Owner,
                        request.Repository,
                        request.SecretName,
                        request.Vendor,
                        request.CodeRepository,
                        request.AccessToken,
                        request.PromptDirectory,
                        request.CodeSource,
                        request.LocalPath
                    );
                    var result = await sender.Send(command, cancellationToken);

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(ConfigureConnector))
            .WithTags("Backlog");

    // Vendor and CodeRepository are optional: omitting the vendor means GitHub, which is what
    // every Connector configured before Azure DevOps existed was, and GitHub has no separate code
    // repository to name.
    //
    // AccessToken is the second path (#124): supply the value and the product names and stores
    // it. Exactly one of it and SecretName arrives; the Validator refuses neither and both.
    // CodeSource is optional the same way Vendor is: absent means Repository, which is what
    // every Connector configured before #210 was. LocalPath travels with LocalFolder only.
    internal sealed record Request(
        string Owner,
        string Repository,
        string? SecretName = null,
        string? Vendor = null,
        string? CodeRepository = null,
        string? AccessToken = null,
        string? PromptDirectory = null,
        string? CodeSource = null,
        string? LocalPath = null
    );

    /// <summary>
    /// Note what is absent: no token. Only ever the name of one, and — when the product wrote it
    /// — when that happened (BR-010 as revised by DEC-052).
    /// </summary>
    internal sealed record Response(
        Guid ProjectId,
        string Vendor,
        string Owner,
        string Repository,
        string SecretName,
        string? CodeRepository,
        DateTimeOffset? SecretSetAt,
        string? PromptDirectory,
        string CodeSource,
        string? LocalPath
    );

    // Admin, declared rather than checked (#13, design D1). This use case is where the product's
    // first two role checks lived — hand-copied, inside the handler, on two of its paths — and a
    // third copy was what the next person needing one would have written. The declaration the
    // pipeline enforces replaces both.
    [Requires(BacklogPermissions.Configure)]
    internal sealed record Command(
        Guid ProjectId,
        string Owner,
        string Repository,
        string? SecretName = null,
        string? Vendor = null,
        string? CodeRepository = null,
        string? AccessToken = null,
        string? PromptDirectory = null,
        string? CodeSource = null,
        string? LocalPath = null
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Owner).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Repository).NotEmpty().MaximumLength(200);

            // Not both — a caller who believes two different things about where the credential lives,
            // and picking one for them would silently ignore the other.
            //
            // "Not neither" is deliberately NOT here (#160, design D1). Whether absent is acceptable
            // depends on whether this project already has a Connector to reuse the credential of, and
            // this validator runs before the handler — it is evaluated where the database is not. The
            // handler decides that one.
            RuleFor(command => command)
                .Must(command =>
                    string.IsNullOrWhiteSpace(command.SecretName)
                    || string.IsNullOrWhiteSpace(command.AccessToken)
                )
                .WithName("credential")
                .WithMessage(
                    "Supply either a token to store or the name of an existing secret, not both."
                );

            RuleFor(command => command.SecretName!)
                .MaximumLength(200)
                .When(command => !string.IsNullOrWhiteSpace(command.SecretName));

            RuleFor(command => command.AccessToken!)
                .MaximumLength(500)
                .When(command => !string.IsNullOrWhiteSpace(command.AccessToken));

            // Unspecified means GitHub, but *misspelled* must not: silently falling back would
            // verify an Azure DevOps organisation against github.com and store the wrong vendor.
            RuleFor(command => command.Vendor!)
                .Must(value => Enum.TryParse<BacklogVendor>(value, out _))
                .When(command => !string.IsNullOrWhiteSpace(command.Vendor))
                .WithMessage(
                    $"Vendor must be one of: {string.Join(", ", Enum.GetNames<BacklogVendor>())}."
                );

            RuleFor(command => command.CodeRepository!)
                .MaximumLength(200)
                .When(command => command.CodeRepository is not null);

            // The prompts directory is a repository path, not a name: length is all this can check,
            // because whether it exists is only knowable at the moment a prompt is read (#150).
            RuleFor(command => command.PromptDirectory!)
                .MaximumLength(200)
                .When(command => command.PromptDirectory is not null);

            // Same shape as Vendor: absent means Repository, misspelled must not silently mean it.
            RuleFor(command => command.CodeSource!)
                .Must(value => Enum.TryParse<CodeSource>(value, ignoreCase: true, out _))
                .When(command => !string.IsNullOrWhiteSpace(command.CodeSource))
                .WithMessage(
                    $"CodeSource must be one of: {string.Join(", ", Enum.GetNames<CodeSource>())}."
                );

            // A local folder without a path is not a configuration; a relative path would move
            // with the worker's working directory, which nobody chose.
            RuleFor(command => command.LocalPath!)
                .NotEmpty()
                .MaximumLength(500)
                .Must(Path.IsPathFullyQualified)
                .When(command =>
                    Enum.TryParse<CodeSource>(command.CodeSource, ignoreCase: true, out var parsed)
                    && parsed == CodeSource.LocalFolder
                )
                .WithMessage("A local folder code source needs an absolute path on the host.");
        }
    }

    internal sealed class Handler(
        BacklogDbContext database,
        IEnumerable<IBacklogConnector> connectors,
        ISecretResolver secrets,
        ISecretStore store,
        IConfiguration configuration,
        TimeProvider clock
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            // OPN-003 is closed and Azure DevOps is registered, so the vendor is a choice now.
            // The Validator has already rejected anything unparseable, so absent means GitHub.
            var vendor = string.IsNullOrWhiteSpace(command.Vendor)
                ? BacklogVendor.GitHub
                : Enum.Parse<BacklogVendor>(command.Vendor);

            // Absent means Repository — what every Connector was before #210.
            var codeSource = string.IsNullOrWhiteSpace(command.CodeSource)
                ? CodeSource.Repository
                : Enum.Parse<CodeSource>(command.CodeSource, ignoreCase: true);

            // Refused before anything is stored or verified: on a deployment that is not
            // somebody's own machine the code-source surface does not exist (#210, DEC-049).
            if (codeSource == CodeSource.LocalFolder && !IdentityHabitat.IsSelfHost(configuration))
            {
                return BacklogErrors.CodeSourceUnavailable();
            }

            // Loaded before the credential is chosen (design D3): reuse needs this Connector's own
            // stored name, and that is not knowable from the request. This replaces the later lookup
            // rather than adding one, and it leaves the store-then-verify ordering below untouched —
            // that ordering is between storing and verifying, and this is a read before both.
            var connector = await database.Connectors.FirstOrDefaultAsync(
                entity => entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            var supplied = !string.IsNullOrWhiteSpace(command.AccessToken);
            var named = !string.IsNullOrWhiteSpace(command.SecretName);
            var reusing = !supplied && !named;

            if (reusing)
            {
                // Nothing stored to fall back on: the refusal reads as it always did.
                if (connector is null)
                {
                    return BacklogErrors.CredentialRequired();
                }

                // The derived name is a function of the vendor, so a switch has no credential to keep.
                if (connector.Vendor != vendor)
                {
                    return BacklogErrors.CredentialRequiredForVendor(
                        connector.Vendor.ToString(),
                        vendor.ToString()
                    );
                }
            }

            // After the reuse decision, deliberately: "this credential belongs to another vendor" is
            // true whether or not the target vendor is wired up, and it is the more useful of the two
            // refusals to receive.
            var implementation = connectors.FirstOrDefault(candidate => candidate.Vendor == vendor);
            if (implementation is null)
            {
                return BacklogErrors.VendorUnavailable($"no connector is registered for {vendor}");
            }

            var secretName =
                supplied ? ConnectorSecret.NameFor(command.ProjectId, vendor)
                : reusing ? connector!.SecretName
                : command.SecretName!;

            if (supplied)
            {
                try
                {
                    await store.Store(secretName, command.AccessToken!, cancellationToken);
                }
                catch (SecretStoreUnavailableException exception)
                {
                    return BacklogErrors.SecretStoreUnavailable(exception.Message);
                }
            }

            // Resolved rather than reused, on both paths. On the supplied path that is the point:
            // verifying with the value we just read back proves the round trip, so a store that
            // truncated it or a habitat whose write did not take is caught here and not at the
            // first poll (design D3).
            string token;
            try
            {
                token = await secrets.Resolve(secretName, cancellationToken);
            }
            catch (SecretNotFoundException)
            {
                return BacklogErrors.SecretNotFound(secretName);
            }

            var coordinates = new BacklogCoordinates(command.Owner, command.Repository);
            var access = await implementation.VerifyAccess(
                coordinates,
                ConnectorProbe.DocumentPath,
                token,
                cancellationToken
            );
            if (!access.Satisfied)
            {
                // A stored value with no Connector referencing it is inert, and the derived name
                // means the next attempt overwrites it. A Connector pointing at a credential
                // nobody verified is the failure UC-004 exists to prevent — this is the right
                // way round.
                // Named, not generic: the refusal says which read failed and repeats the
                // vendor's own reason, because that is what tells the Admin what to grant
                // (#132, design D2).
                return access.FirstRefusal;
            }

            if (connector is null)
            {
                connector = Connector.Create(
                    command.ProjectId,
                    vendor,
                    command.Owner,
                    command.Repository,
                    secretName
                );
                database.Connectors.Add(connector);
            }
            else
            {
                // At most one Connector per Project: reconfigure in place rather than add.
                connector.Reconfigure(vendor, command.Owner, command.Repository, secretName);
            }

            if (supplied)
            {
                connector.RecordSecretStored(clock.GetUtcNow());
            }

            // Set on both paths, so clearing the field on a reconfigure actually clears it.
            connector.UseCodeRepository(
                string.IsNullOrWhiteSpace(command.CodeRepository) ? null : command.CodeRepository
            );

            // Blank clears it back to the default rather than storing "", so one value means one
            // thing: null is "wherever prompts live by convention" (design D6).
            connector.UsePromptDirectory(
                string.IsNullOrWhiteSpace(command.PromptDirectory)
                    ? null
                    : command.PromptDirectory.Trim().Trim('/')
            );

            // Set on both paths, like the fields above: reconfiguring without naming a code
            // source is choosing Repository, and a stale local path must not survive that.
            if (codeSource == CodeSource.LocalFolder)
            {
                connector.UseLocalFolder(command.LocalPath!.Trim());
            }
            else
            {
                connector.UseRepositorySource();
            }

            await database.SaveChangesAsync(cancellationToken);

            return new Response(
                connector.ProjectId,
                connector.Vendor.ToString(),
                connector.Owner,
                connector.Repository,
                connector.SecretName,
                connector.CodeRepository,
                connector.SecretSetAt,
                connector.PromptDirectory,
                connector.CodeSource.ToString(),
                connector.LocalPath
            );
        }
    }
}
