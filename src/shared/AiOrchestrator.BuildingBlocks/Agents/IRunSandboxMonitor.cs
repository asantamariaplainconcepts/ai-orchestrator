namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Which executing Runs have a sandbox a human could open a shell in, and what that sandbox is
/// called (#304). The preview monitor's sibling, and it exists for the same reason: the Runs module
/// offers the terminal and must never speak to a sandbox itself — it reads a snapshot, and the
/// process holding the sandboxes keeps that snapshot true.
/// <para>
/// A process that launches no sandboxes answers <i>not hosted</i> rather than "no such Run". The
/// distinction is the whole point (ADR-0021): a terminal is permitted in self-host and refused in a
/// deployment, so "no terminal is hosted here" is a habitat's honest answer and must not read as a
/// Run that failed to offer one — nor as a permission a Member could ask to be given.
/// </para>
/// </summary>
public interface IRunSandboxMonitor
{
    /// <summary>
    /// The name of the sandbox this Run is executing in right now, or null when there is none —
    /// because the Run is not executing, or because sandboxes are not hosted here.
    /// <see cref="Hosted"/> distinguishes the last case.
    /// </summary>
    string? NameFor(Guid runId);

    /// <summary>
    /// The Run using the sandbox by this name, or null when none of this process's Runs is (#311) — the
    /// reverse question, asked by the surface that starts from a sandbox rather than from a Run.
    /// <para>
    /// Null is a fact rather than a failed lookup: this ledger holds only what this process created, so a
    /// sandbox abandoned by an earlier process is genuinely owned by no Run. Persisting the mapping to
    /// close that gap would reintroduce the lie the ledger exists to avoid — a stored name that outlives
    /// the sandbox it describes.
    /// </para>
    /// </summary>
    Guid? RunUsing(string sandbox);

    /// <summary>Whether this process is the one that would hold sandboxes at all.</summary>
    bool Hosted { get; }
}

/// <summary>
/// The default every habitat starts from; a host that launches local sandboxes replaces it.
/// Registered by the Runs module so the terminal's surface always resolves — the ability is absent,
/// never the answer.
/// </summary>
public sealed class UnhostedRunSandboxMonitor : IRunSandboxMonitor
{
    public string? NameFor(Guid runId) => null;

    public Guid? RunUsing(string sandbox) => null;

    public bool Hosted => false;
}
