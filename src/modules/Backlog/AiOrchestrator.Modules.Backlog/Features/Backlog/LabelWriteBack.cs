using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// UC-008's ordering, made structural (design D1): write the label at the vendor first, then
/// re-synchronise the mirror through the same path polling uses. The mirror never claims a
/// label the vendor rejected, and the resulting <c>StoryChanged</c> comes from the ordinary
/// reconciler — portal labelling and vendor labelling are one mechanism (DEC-027).
/// </summary>
sealed class LabelWriteBack(ConnectorAccess access, BacklogSynchroniser synchroniser)
{
    public Task<ErrorOr<int>> Apply(
        Guid projectId,
        string vendorStoryId,
        string label,
        CancellationToken cancellationToken
    ) => Write(projectId, vendorStoryId, label, apply: true, cancellationToken);

    public Task<ErrorOr<int>> Remove(
        Guid projectId,
        string vendorStoryId,
        string label,
        CancellationToken cancellationToken
    ) => Write(projectId, vendorStoryId, label, apply: false, cancellationToken);

    async Task<ErrorOr<int>> Write(
        Guid projectId,
        string vendorStoryId,
        string label,
        bool apply,
        CancellationToken cancellationToken
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return context.Errors;
        }

        var (implementation, coordinates, token) = context.Value;

        var written = apply
            ? await implementation.ApplyLabel(
                coordinates,
                vendorStoryId,
                label,
                token,
                cancellationToken
            )
            : await implementation.RemoveLabel(
                coordinates,
                vendorStoryId,
                label,
                token,
                cancellationToken
            );

        if (written.IsError)
        {
            // The vendor refused; the mirror was never touched — stale-not-lying by construction.
            return written.Errors;
        }

        // The ordinary sync: mirror update AND the StoryChanged event both come from the
        // reconciler, exactly as if the label had been applied at the vendor.
        return await synchroniser.Synchronise(projectId, cancellationToken);
    }
}
