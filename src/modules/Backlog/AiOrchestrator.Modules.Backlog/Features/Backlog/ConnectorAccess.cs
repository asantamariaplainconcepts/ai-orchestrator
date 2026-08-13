using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// "Give me a usable Connector for this Project" — the sequence every vendor call needs:
/// find the stored Connector, pick the implementation for its vendor, resolve the credential
/// by name (BR-010). Extracted once three call sites wanted it; the refusals stay identical
/// because there is now one place that produces them.
/// </summary>
sealed class ConnectorAccess(
    BacklogDbContext database,
    IEnumerable<IBacklogConnector> connectors,
    IConnectorCredentialResolver credentials
)
{
    public async Task<ErrorOr<ConnectorContext>> Resolve(
        Guid projectId,
        CancellationToken cancellationToken
    )
    {
        var connector = await database.Connectors.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId,
            cancellationToken
        );

        if (connector is null)
        {
            return BacklogErrors.ConnectorNotFound(projectId);
        }

        var implementation = connectors.FirstOrDefault(candidate =>
            candidate.Vendor == connector.Vendor
        );
        if (implementation is null)
        {
            return BacklogErrors.VendorUnavailable(
                $"no connector is registered for {connector.Vendor}"
            );
        }

        // Which source is the Connector's own answer (DEC-069), so the poller and this cannot
        // drift into two readings of one row.
        var reference = connector.Credential();
        if (reference is null)
        {
            return BacklogErrors.SecretNotFound(connector.SecretName ?? string.Empty);
        }

        try
        {
            var credential = await credentials.Resolve(reference, cancellationToken);
            return new ConnectorContext(
                implementation,
                new BacklogCoordinates(connector.Owner, connector.Repository),
                credential.Token
            )
            {
                PromptDirectory = connector.PromptDirectory,
                CodeSource = connector.CodeSource,
                CredentialSource = credential.Source,
            };
        }
        catch (SecretNotFoundException)
        {
            return BacklogErrors.SecretNotFound(connector.SecretName ?? string.Empty);
        }
        catch (HostCredentialUnavailableException unavailable)
        {
            // Named separately from a missing secret because the two have nothing in common to
            // fix: one is a value nobody stored, the other is a machine nobody logged in.
            return BacklogErrors.HostCredentialUnavailable(unavailable.Message);
        }
    }
}

/// <summary>The token is a value in memory for the length of one call — never stored (BR-010).</summary>
sealed record ConnectorContext(
    IBacklogConnector Connector,
    BacklogCoordinates Coordinates,
    string Token
)
{
    /// <summary>
    /// Where this project's prompts live, or null for the convention (#150). Deliberately not a
    /// positional parameter: three call sites deconstruct this record, and widening the deconstruction
    /// would have made every one of them mention a directory it has no use for.
    /// </summary>
    public string? PromptDirectory { get; init; }

    /// <summary>
    /// Which capabilities this Connector's configuration will exercise depends on it (#226): a
    /// local folder's working copy is the host's own, so nothing here pushes or opens a pull
    /// request. Carried for the same reason the directory is — a positional parameter would make
    /// every call site mention a value most of them have no use for.
    /// </summary>
    public CodeSource CodeSource { get; init; }

    /// <summary>
    /// Which identity this call authenticates as — the named secret, or the host's credential
    /// helper and the host it was asked about. Carried so a record of the call can name it rather
    /// than leave it to inference (ADR-0028; BR-014), which is exactly what
    /// <c>IAgentProcessHost.CredentialSource</c> already does for the agent's own process.
    /// </summary>
    public CredentialSource? CredentialSource { get; init; }
}
