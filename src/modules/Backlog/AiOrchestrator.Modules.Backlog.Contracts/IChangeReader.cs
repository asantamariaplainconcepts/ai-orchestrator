namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The open changes of a project's connected repository, for modules that must not reach the
/// Backlog implementation (inbox-open-prs) — the Runs module reads through Contracts, as it does
/// for Stories, Connectors and change files.
/// </summary>
public interface IChangeReader
{
    /// <summary>
    /// Read live and never stored (BR-008). A project with no Connector answers empty — nothing to
    /// list is a state, not a failure — and a vendor refusal arrives as <see cref="OpenChanges.Reason"/>
    /// so the caller can show why instead of a blank.
    /// </summary>
    Task<OpenChanges> Open(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="Reason"/> is null when the read worked; the list is then complete, possibly empty.
/// When it is set, the list is empty and the reason is what the caller renders.
/// </summary>
public sealed record OpenChanges(IReadOnlyList<OpenChangeView> Changes, string? Reason)
{
    public static OpenChanges None { get; } = new([], Reason: null);

    public static OpenChanges Refused(string reason) => new([], reason);
}

/// <summary>
/// One open change, vendor-neutrally. <see cref="HeadBranch"/> is the branch **name** — unlike a
/// linked change's head SHA — because recognising a product branch (<c>run/{id}</c>) is a caller's
/// legitimate question and a SHA cannot answer it.
/// </summary>
public sealed record OpenChangeView(
    int Number,
    string Title,
    string Url,
    string HeadBranch,
    DateTimeOffset CreatedAt
);
