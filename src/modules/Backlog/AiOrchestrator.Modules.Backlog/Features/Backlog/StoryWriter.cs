using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts write surface, implemented by the owner of the Connector.</summary>
sealed class StoryWriter(ConnectorAccess access) : IStoryWriter
{
    /// <summary>One estimate per Story: the prefix is what makes "replace" possible.</summary>
    public const string EstimatePrefix = "estimate:";

    public Task<string?> AddComment(
        Guid projectId,
        string vendorStoryId,
        string comment,
        CancellationToken cancellationToken = default
    ) =>
        With(
            projectId,
            (connector, coordinates, token) =>
                connector.AddComment(coordinates, vendorStoryId, comment, token, cancellationToken)
        );

    public Task<string?> ApplyLabel(
        Guid projectId,
        string vendorStoryId,
        string label,
        CancellationToken cancellationToken = default
    ) =>
        With(
            projectId,
            (connector, coordinates, token) =>
                connector.ApplyLabel(coordinates, vendorStoryId, label, token, cancellationToken)
        );

    public Task<string?> SetState(
        Guid projectId,
        string vendorStoryId,
        string state,
        CancellationToken cancellationToken = default
    ) =>
        With(
            projectId,
            (connector, coordinates, token) =>
                connector.SetState(coordinates, vendorStoryId, state, token, cancellationToken)
        );

    public async Task<string?> SetEstimate(
        Guid projectId,
        string vendorStoryId,
        int estimate,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return context.FirstError.Description;
        }

        var (connector, coordinates, token) = context.Value;

        // Replace, not add — and read the labels from the VENDOR, not the Mirror. The Mirror
        // is refreshed by polling, so immediately after a Run changed something it still shows
        // the old world; replacing against it leaves two estimates on a Story whenever two
        // estimates happen between polls (BR-008, design D3/D5).
        var current = await connector.FetchStory(
            coordinates,
            vendorStoryId,
            token,
            cancellationToken
        );
        if (current.IsError)
        {
            return current.FirstError.Description;
        }

        IReadOnlyList<string> existing = current.Value?.Labels ?? [];

        foreach (
            var stale in existing.Where(label =>
                label.StartsWith(EstimatePrefix, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var removed = await connector.RemoveLabel(
                coordinates,
                vendorStoryId,
                stale,
                token,
                cancellationToken
            );
            if (removed.IsError)
            {
                return removed.FirstError.Description;
            }
        }

        var applied = await connector.ApplyLabel(
            coordinates,
            vendorStoryId,
            $"{EstimatePrefix}{estimate}",
            token,
            cancellationToken
        );

        return applied.IsError ? applied.FirstError.Description : null;
    }

    async Task<string?> With(
        Guid projectId,
        Func<
            Connectors.IBacklogConnector,
            Connectors.BacklogCoordinates,
            string,
            Task<ErrorOr.ErrorOr<ErrorOr.Success>>
        > act
    )
    {
        var context = await access.Resolve(projectId, CancellationToken.None);
        if (context.IsError)
        {
            return context.FirstError.Description;
        }

        var (connector, coordinates, token) = context.Value;
        var result = await act(connector, coordinates, token);

        return result.IsError ? result.FirstError.Description : null;
    }
}
