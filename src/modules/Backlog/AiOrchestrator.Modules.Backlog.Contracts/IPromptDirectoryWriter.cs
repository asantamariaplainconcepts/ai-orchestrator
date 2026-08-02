namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// Saving the directory a project's prompts live in, for the one caller that learns it by looking
/// (#229): setting a project up discovers where the pipeline already is, and the Admin's
/// confirmation of that answer is a Connector setting.
/// <para>
/// Deliberately one verb and not the whole Connector. The alternative was to have the setup action
/// call the configure endpoint, which would mean re-sending owner, repository and the credential's
/// name — and a save that re-sends a credential's name is a save that can drop it.
/// </para>
/// </summary>
public interface IPromptDirectoryWriter
{
    /// <summary>
    /// Returns false when the project has no Connector — the caller reports that rather than
    /// treating it as an error, because a project with no Connector has nothing to discover in.
    /// </summary>
    Task<bool> UseDirectory(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    );
}
