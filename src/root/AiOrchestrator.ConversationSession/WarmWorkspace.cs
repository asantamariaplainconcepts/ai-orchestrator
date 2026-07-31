using AiOrchestrator.BuildingBlocks.Agents;
using ErrorOr;

namespace AiOrchestrator.ConversationSession;

/// <summary>
/// The clone, kept (#166, design D2).
/// <para>
/// This container belongs to one conversation for as long as the conversation is being used, so the
/// repository is checked out on the first message and reused by every later one. That is the whole
/// reason this shape was chosen over a job per message: a fresh clone per question is ten seconds a
/// person waits for, every time, to be handed the same files.
/// </para>
/// <para>
/// Guarded rather than merely cached: two messages can overlap if somebody sends while the previous
/// pass is still running, and two clones into the same directory is a corrupted checkout rather than
/// a slow one.
/// </para>
/// </summary>
sealed class WarmWorkspace(ICodeWorkspace workspace) : IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The conversation's own id, stable for the life of this container.</summary>
    readonly Guid _checkout = Guid.CreateVersion7();

    ErrorOr<string>? _path;

    public async Task<ErrorOr<string>> PathFor(
        CodeCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (_path is { IsError: false } ready)
        {
            return ready;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Re-checked inside the gate: two overlapping first messages both saw no path.
            if (_path is { IsError: false } settled)
            {
                return settled;
            }

            var prepared = await workspace.Prepare(
                coordinates,
                _checkout,
                token,
                cancellationToken
            );

            // A failed clone is not cached. The credential may have been rotated a minute ago, and
            // remembering the failure would make the conversation permanently broken for a reason
            // that has already been fixed.
            _path = prepared.IsError ? (ErrorOr<string>)prepared.FirstError : prepared.Value.Path;

            return _path.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
