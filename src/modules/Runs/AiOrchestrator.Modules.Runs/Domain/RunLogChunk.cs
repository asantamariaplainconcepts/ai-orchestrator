namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// One appended slice of a Run's output (#96). The durable store IS the stream (design D1):
/// every committed chunk survives a crash, so a partial log up to the crash is simply the rows
/// written so far — BR-014 with no window/record split to reconcile.
/// </summary>
sealed class RunLogChunk
{
    RunLogChunk() { }

    public RunLogChunk(Guid runId, int sequence, string content, DateTimeOffset at)
    {
        RunId = runId;
        Sequence = sequence;
        Content = content;
        At = at;
    }

    public long Id { get; private set; }

    public Guid RunId { get; private set; }

    /// <summary>Writer-assigned order — arrival order, which is the only order a log has.</summary>
    public int Sequence { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset At { get; private set; }
}
