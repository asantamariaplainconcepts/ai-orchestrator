using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// One poll: resolve the credential, fetch, reconcile, record the outcome.
/// <para>
/// Shared by the on-demand refresh and the background poller so both take exactly the same path —
/// the deterministic one the tests drive is the one production runs.
/// </para>
/// </summary>
sealed class BacklogSynchroniser(
    BacklogDbContext database,
    IEnumerable<IBacklogConnector> connectors,
    ISecretResolver secrets,
    TimeProvider clock
)
{
    public async Task<ErrorOr<int>> Synchronise(Guid projectId, CancellationToken cancellationToken)
    {
        var connector = await database.Connectors.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId,
            cancellationToken
        );

        if (connector is null)
        {
            return BacklogErrors.ConnectorNotFound(projectId);
        }

        var result = await Fetch(connector, cancellationToken);

        if (result.IsError)
        {
            // A failed poll must degrade to stale, never to empty: the mirror is left untouched
            // and the reason is recorded so the UI can say "we could not look" rather than
            // "there is nothing here".
            connector.RecordFailure(clock.GetUtcNow(), result.FirstError.Description);
            await database.SaveChangesAsync(cancellationToken);
            return result.Errors;
        }

        var changed = await Reconcile(projectId, result.Value.Stories, cancellationToken);
        connector.RecordSuccess(clock.GetUtcNow());

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsConcurrentReconcile(exception))
        {
            // Another refresh for this Project reconciled the same Stories concurrently and won
            // the race on the (ProjectId, VendorId) index. Reconciliation is one transaction, so
            // this one rolled back whole and the winner's result is already complete and correct
            // — there is nothing to repair and nothing to report. Reported as zero changes
            // because this call made none.
            //
            // The unique index is what makes "one Story per vendor id" true rather than hoped
            // for; handling the conflict is the fix, removing the index would not be.
            return 0;
        }

        return changed;

        // Narrow on purpose: only a unique-key violation means "someone else already did this".
        // Catching DbUpdateException broadly would swallow genuine write failures — a deadlock, a
        // constraint we actually violated — and turn them into silent successes.
        static bool IsConcurrentReconcile(DbUpdateException exception) =>
            exception.InnerException
                is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    async Task<ErrorOr<BacklogSnapshot>> Fetch(
        Connector connector,
        CancellationToken cancellationToken
    )
    {
        var implementation = connectors.FirstOrDefault(candidate =>
            candidate.Vendor == connector.Vendor
        );
        if (implementation is null)
        {
            return BacklogErrors.VendorUnavailable(
                $"no connector is registered for {connector.Vendor}"
            );
        }

        string token;
        try
        {
            token = await secrets.Resolve(connector.SecretName, cancellationToken);
        }
        catch (SecretNotFoundException)
        {
            return BacklogErrors.SecretNotFound(connector.SecretName);
        }

        var coordinates = new BacklogCoordinates(connector.Owner, connector.Repository);
        return await implementation.FetchStories(coordinates, token, cancellationToken);
    }

    /// <summary>
    /// Full reconciliation against what the vendor currently reports (BR-008): upsert what is
    /// present, remove what is absent. Identity is the vendor id, so a renamed Story stays the
    /// same Story rather than becoming a new one plus a deletion.
    /// </summary>
    async Task<int> Reconcile(
        Guid projectId,
        IReadOnlyList<VendorStory> incoming,
        CancellationToken cancellationToken
    )
    {
        var existing = await database
            .Stories.Where(story => story.ProjectId == projectId)
            .ToDictionaryAsync(story => story.VendorId, cancellationToken);

        var seenAt = clock.GetUtcNow();
        var changes = 0;

        foreach (var vendorStory in incoming)
        {
            if (existing.TryGetValue(vendorStory.VendorId, out var story))
            {
                if (
                    story.UpdateFrom(
                        vendorStory.Title,
                        vendorStory.State,
                        vendorStory.Labels,
                        seenAt
                    )
                )
                {
                    changes++;
                }

                existing.Remove(vendorStory.VendorId);
            }
            else
            {
                database.Stories.Add(
                    Story.Create(
                        projectId,
                        vendorStory.VendorId,
                        vendorStory.Title,
                        vendorStory.State,
                        vendorStory.Labels,
                        seenAt
                    )
                );
                changes++;
            }
        }

        // Whatever the vendor no longer returns is no longer in the backlog.
        if (existing.Count > 0)
        {
            database.Stories.RemoveRange(existing.Values);
            changes += existing.Count;
        }

        return changes;
    }
}
