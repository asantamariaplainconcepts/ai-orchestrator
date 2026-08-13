namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The Connector as other modules may know it: coordinates and the credential's <b>name</b>
/// (BR-010 — the value is resolved by whoever holds a vault identity, never read from here).
/// </summary>
public interface IConnectorReader
{
    Task<ConnectorSnapshot?> Find(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="CodeSource"/> is the enum's name (<c>Repository</c> | <c>LocalFolder</c>) and
/// <paramref name="LocalPath"/> travels only with the latter (#210) — the Runs module derives a
/// Run's default execution locus from it and the executor picks the workspace by it.
/// </summary>
public sealed record ConnectorSnapshot(
    string Vendor,
    string Owner,
    string Repository,
    string? SecretName,
    string CodeSource,
    string? LocalPath
)
{
    /// <summary>
    /// Where this project's prompts live, or null for the convention (#150). Not positional:
    /// discovery (#229) is the only caller that wants it, and widening the constructor would make
    /// every other call site mention a directory it has no use for.
    /// </summary>
    public string? PromptDirectory { get; init; }

    /// <summary>
    /// The command that makes a Local Run's fresh checkout buildable, or null for none (#332).
    /// Travels with <see cref="LocalPath"/> and is meaningless without it. Not positional, for the
    /// same reason as the prompts directory: the executor is the only caller that wants it.
    /// </summary>
    public string? LocalSetupCommand { get; init; }
}
