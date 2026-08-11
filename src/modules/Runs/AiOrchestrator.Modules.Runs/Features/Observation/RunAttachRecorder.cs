using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// Records that a human opened a terminal on a Run (#304, criterion 6). A Run's sandbox carries the
/// machine owner's own session (#288), so a human working inside it may act with the owner's
/// credentials — a capability that left no trace of who used it could not be reasoned about
/// afterwards, which is the whole reason Members were granted it at all.
/// </summary>
interface IRunAttachRecorder
{
    Task Attached(Guid runId, string who, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>
/// Writes the attach into the Run's own log, as one line, beside everything else that Run did.
/// <para>
/// <b>The attach, and never the session.</b> What a person typed does not go here: the log is what
/// <c>transcript.ts</c> renders, and a terminal's bytes would arrive as escape sequences that parse
/// as nothing — turning a Run's record into a screen capture. That distinction is the point of this
/// slice, so it is enforced by there being no code that could write them.
/// </para>
/// </summary>
sealed class RunAttachRecorder(RunsDbContext database) : IRunAttachRecorder
{
    public async Task Attached(
        Guid runId,
        string who,
        DateTimeOffset at,
        CancellationToken cancellationToken
    )
    {
        // Appended after whatever the agent has written so far. A racing agent may take the same
        // sequence; the log reads by sequence and then by id, so the worst case is two lines sharing
        // a number and keeping their order — which is better than a terminal that refuses to open
        // because a log line clashed.
        var next = await database
            .Set<RunLogChunk>()
            .Where(chunk => chunk.RunId == runId)
            .Select(chunk => (int?)chunk.Sequence)
            .MaxAsync(cancellationToken);

        database.Add(
            new RunLogChunk(
                runId,
                (next ?? 0) + 1,
                $"[terminal] {who} opened a shell in this run's sandbox",
                at
            )
        );

        await database.SaveChangesAsync(cancellationToken);
    }
}
