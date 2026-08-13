using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// Configuring a Connector from outside the Backlog module (#347) — the <c>IStoryWriter</c> /
/// <c>IPromptDirectoryWriter</c> shape, because the Projects module owns the create-a-Project flow
/// and structurally cannot reach Backlog's internals (MOD001-005).
/// <para>
/// Deliberately narrow: this is not "configure any Connector any way". It is the one composition a
/// named folder produces — derived coordinates, the local folder as the code source, and the host's
/// own credential — so the general configuration surface stays where its validator, its Admin gate
/// and its credential rules already live.
/// </para>
/// </summary>
public interface IConnectorWriter
{
    /// <summary>
    /// Creates the Connector a named folder implies, verifying against the live vendor <b>before</b>
    /// storing anything — a Connector that exists is one that works (UC-004).
    /// <para>
    /// Refuses where the habitat cannot authenticate as its host, so the self-host-only rule holds
    /// at the seam and not only in the caller (DEC-069).
    /// </para>
    /// </summary>
    Task<ErrorOr<Success>> CreateFromLocalFolder(
        Guid projectId,
        LocalFolderConnector connector,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// What the folder yielded. <paramref name="CodeRepository"/> is null for GitHub and the repository
/// inside the project for Azure DevOps.
/// </summary>
public sealed record LocalFolderConnector(
    string Vendor,
    string Owner,
    string Repository,
    string? CodeRepository,
    string LocalPath
);
