using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
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
                        request.CodeRepository
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
    internal sealed record Request(
        string Owner,
        string Repository,
        string SecretName,
        string? Vendor = null,
        string? CodeRepository = null
    );

    /// <summary>Note what is absent: no token. Only ever the name of one (BR-010).</summary>
    internal sealed record Response(
        Guid ProjectId,
        string Vendor,
        string Owner,
        string Repository,
        string SecretName,
        string? CodeRepository
    );

    internal sealed record Command(
        Guid ProjectId,
        string Owner,
        string Repository,
        string SecretName,
        string? Vendor = null,
        string? CodeRepository = null
    ) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Owner).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Repository).NotEmpty().MaximumLength(200);
            RuleFor(command => command.SecretName).NotEmpty().MaximumLength(200);

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
        }
    }

    internal sealed class Handler(
        BacklogDbContext database,
        IEnumerable<IBacklogConnector> connectors,
        ISecretResolver secrets
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

            var implementation = connectors.FirstOrDefault(candidate => candidate.Vendor == vendor);
            if (implementation is null)
            {
                return BacklogErrors.VendorUnavailable($"no connector is registered for {vendor}");
            }

            string token;
            try
            {
                token = await secrets.Resolve(command.SecretName, cancellationToken);
            }
            catch (SecretNotFoundException)
            {
                return BacklogErrors.SecretNotFound(command.SecretName);
            }

            var coordinates = new BacklogCoordinates(command.Owner, command.Repository);
            var access = await implementation.VerifyAccess(coordinates, token, cancellationToken);
            if (access.IsError)
            {
                return access.Errors;
            }

            var connector = await database.Connectors.FirstOrDefaultAsync(
                entity => entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            if (connector is null)
            {
                connector = Connector.Create(
                    command.ProjectId,
                    vendor,
                    command.Owner,
                    command.Repository,
                    command.SecretName
                );
                database.Connectors.Add(connector);
            }
            else
            {
                // At most one Connector per Project: reconfigure in place rather than add.
                connector.Reconfigure(
                    vendor,
                    command.Owner,
                    command.Repository,
                    command.SecretName
                );
            }

            // Set on both paths, so clearing the field on a reconfigure actually clears it.
            connector.UseCodeRepository(
                string.IsNullOrWhiteSpace(command.CodeRepository) ? null : command.CodeRepository
            );

            await database.SaveChangesAsync(cancellationToken);

            return new Response(
                connector.ProjectId,
                connector.Vendor.ToString(),
                connector.Owner,
                connector.Repository,
                connector.SecretName,
                connector.CodeRepository
            );
        }
    }
}
