using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// The Contracts write surface for the one Connector a named folder implies (#347).
/// <para>
/// It repeats <c>ConfigureConnector</c>'s ordering rather than its breadth — resolve, verify live,
/// then store — because that ordering is the guarantee (UC-004), not an implementation detail. What
/// it does not repeat is the credential choice: a folder-derived Connector always takes the host
/// path, so there is no token to store, no secret to name, and no "neither or both" to adjudicate.
/// </para>
/// </summary>
sealed class ConnectorWriter(
    BacklogDbContext database,
    IEnumerable<IBacklogConnector> connectors,
    IConnectorCredentialResolver credentials,
    IConfiguration configuration
) : IConnectorWriter
{
    public async Task<ErrorOr<Success>> CreateFromLocalFolder(
        Guid projectId,
        LocalFolderConnector requested,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(requested);

        // The habitat rule holds at the seam, not only in the caller: a deployment has no host
        // identity to borrow, and the ability is absent there rather than refused (DEC-069).
        if (!IdentityHabitat.IsSelfHost(configuration))
        {
            return BacklogErrors.CodeSourceUnavailable();
        }

        if (IdentityHabitat.LocalFolderUnavailableReason(configuration) is { } declared)
        {
            // Verbatim, the same sentence the capabilities read and the Run refusal carry (#247).
            return BacklogErrors.LocalFolderUnavailable(declared);
        }

        if (!Enum.TryParse<BacklogVendor>(requested.Vendor, out var vendor))
        {
            return BacklogErrors.VendorUnavailable($"'{requested.Vendor}' is not a known vendor");
        }

        var implementation = connectors.FirstOrDefault(candidate => candidate.Vendor == vendor);
        if (implementation is null)
        {
            return BacklogErrors.VendorUnavailable($"no connector is registered for {vendor}");
        }

        if (
            await database.Connectors.AnyAsync(
                entity => entity.ProjectId == projectId,
                cancellationToken
            )
        )
        {
            // A Project has at most one Connector. Reaching here means the Project was created with
            // a folder and already had one, which the create flow cannot produce — refusing beats
            // silently replacing configuration nobody asked to change.
            return BacklogErrors.CredentialInputAmbiguous(
                "this project already has a Connector; configure it on the Connector form instead"
            );
        }

        var reference = CredentialReference.Host(VendorCredentialHosts.For(vendor));

        string token;
        try
        {
            token = (await credentials.Resolve(reference, cancellationToken)).Token;
        }
        catch (HostCredentialUnavailableException unavailable)
        {
            return BacklogErrors.HostCredentialUnavailable(unavailable.Message);
        }

        // Live, against the coordinates the folder produced — so a folder whose `origin` points
        // somewhere this machine's credentials cannot reach is refused naming both.
        var access = await implementation.VerifyAccess(
            new BacklogCoordinates(requested.Owner, requested.Repository),
            ConnectorCapabilities.For(CodeSource.LocalFolder),
            token,
            cancellationToken
        );

        if (!access.Satisfied)
        {
            return access.FirstRefusal;
        }

        var connector = Connector.CreateOnHostCredential(
            projectId,
            vendor,
            requested.Owner,
            requested.Repository
        );

        connector.UseCodeRepository(
            string.IsNullOrWhiteSpace(requested.CodeRepository) ? null : requested.CodeRepository
        );
        connector.UseLocalFolder(requested.LocalPath, setupCommand: null);

        database.Connectors.Add(connector);
        await database.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
