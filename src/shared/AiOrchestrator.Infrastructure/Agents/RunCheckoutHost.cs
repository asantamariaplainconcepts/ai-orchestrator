using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The checkouts this process is currently executing Runs in (#358): one entry per executing Run,
/// written when its working directory is handed to the agent and removed when the agent exits.
/// <para>
/// <see cref="RunSandboxHost"/>'s opposite number, deliberately down to the shape — it answers the
/// same two questions (<i>what does this Run have</i>, <i>whose is this</i>) for a habitat that has
/// checkouts instead of sandboxes. It satisfies <see cref="IRunSandboxMonitor"/> because the Runs
/// module's question is not really "what sandbox" but "is there something a terminal can open, and what
/// is it called": answering that with a checkout path is the honest local answer.
/// </para>
/// <para>
/// In memory for the reason its sibling is: a stored row would outlive the checkout it names and lie
/// after a restart, and a path that no longer resolves is worse than no path — it points a terminal at
/// a directory that has been reaped.
/// </para>
/// <para>
/// The pairing was a parameter nobody kept until this existed. <c>LocalAgentProcessHost.Run</c>
/// received both the working directory and the <c>runId</c> and used neither together, and its comment
/// said so: <i>"Ignored: a child of this process has no sandbox to open a shell in"</i>. That was true
/// until DEC-070; publishing here is what makes "this Run has a terminal" a property rather than a
/// branch somebody has to remember to write.
/// </para>
/// </summary>
public sealed class RunCheckoutHost : IRunSandboxMonitor
{
    readonly ConcurrentDictionary<Guid, string> _checkouts = new();

    public bool Hosted => true;

    public string? NameFor(Guid runId) =>
        _checkouts.TryGetValue(runId, out var checkout) ? checkout : null;

    /// <summary>
    /// The Run working in this checkout, or null when none of this process's Runs is.
    /// <para>
    /// Absence is a fact rather than a failed lookup, exactly as it is for a sandbox: this ledger holds
    /// only what this process handed out, so a checkout left behind by a previous process is genuinely
    /// owned by no Run. <see cref="LocalCheckoutReaper"/> is what removes those, not this.
    /// </para>
    /// </summary>
    public Guid? RunUsing(string checkout)
    {
        foreach (var pair in _checkouts)
        {
            if (string.Equals(pair.Value, checkout, StringComparison.Ordinal))
            {
                return pair.Key;
            }
        }

        return null;
    }

    /// <summary>This Run's checkout is addressable until its agent exits.</summary>
    public void Created(Guid runId, string checkout) => _checkouts[runId] = checkout;

    /// <summary>The agent has gone, so the address has. Idempotent — disposal can race a crash.</summary>
    public void Gone(Guid runId) => _checkouts.TryRemove(runId, out _);

    /// <summary>
    /// Every checkout this process currently has a Run in, as a terminal's surface sees them. Unlike the
    /// sandbox listing this does <b>not</b> ask the machine: a checkout is not a registered object with a
    /// status, it is a directory, so the only honest source is this ledger. The consequence is stated
    /// rather than hidden — a checkout abandoned by a previous process is invisible here, where an
    /// abandoned sandbox is not.
    /// </summary>
    public IReadOnlyList<TerminalTarget> Targets() =>
        [
            .. _checkouts.Select(pair => new TerminalTarget(
                Name: pair.Value,
                Status: "running",
                RunId: pair.Key,
                Workspace: pair.Value
            )),
        ];
}
