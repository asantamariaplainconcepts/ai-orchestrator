namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// One human opening a shell in one sandbox (#311, criterion 7).
/// <para>
/// <b>Why this exists when #304's Run log line already did.</b> That line lives in
/// <see cref="RunLogChunk"/> and is keyed by a Run. The sandboxes surface reaches sandboxes with no
/// Run at all — the one an earlier process abandoned — and those are precisely the attaches least able
/// to be reconstructed afterwards, so they are the ones least safe to leave untraced. A record that
/// only worked when a Run happened to exist would cover the easy half.
/// </para>
/// <para>
/// <see cref="RunId"/> is therefore nullable by design and not by omission: null is the true answer for
/// a sandbox no live Run owns, and the ledger that would fill it in is deliberately unpersisted.
/// </para>
/// <para>
/// The attach, and never the session: what a person typed does not come here, for #304's reason — a
/// terminal's bytes would turn a record into a screen capture. That is enforced by there being no
/// column to put them in.
/// </para>
/// </summary>
sealed class SandboxAttach
{
    SandboxAttach() { }

    public SandboxAttach(string sandbox, string who, DateTimeOffset at, Guid? runId)
    {
        Sandbox = sandbox;
        Who = who;
        At = at;
        RunId = runId;
    }

    public long Id { get; private set; }

    /// <summary>The sandbox entered, which is the only identity an attach always has.</summary>
    public string Sandbox { get; private set; } = string.Empty;

    public string Who { get; private set; } = string.Empty;

    public DateTimeOffset At { get; private set; }

    /// <summary>The Run whose sandbox this was, where one was executing it. Null is a fact.</summary>
    public Guid? RunId { get; private set; }
}
