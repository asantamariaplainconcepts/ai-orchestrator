namespace AiOrchestrator.Modules.Runs;

/// <summary>
/// What can be done with a Project's Runs (BR-009). ACT-002's list is unusually explicit here —
/// view runs, logs and cost; trigger <i>Run now</i>; approve plans; cancel runs — so these
/// permissions are named to match it one for one, and the Member grant reads as that sentence.
/// </summary>
static class RunPermissions
{
    /// <summary>See Runs, their logs, their file changes and their cost.</summary>
    public const string Read = "run.read";

    /// <summary>Start one now (UC-012, DEC-035) — available to both bundles by decision.</summary>
    public const string Trigger = "run.trigger";

    /// <summary>Approve or reject a plan at the gate (UC-011).</summary>
    public const string Approve = "run.approve";

    /// <summary>Stop one that is running (UC-019).</summary>
    public const string Cancel = "run.cancel";

    /// <summary>
    /// Hold a conversation with an agent about this project (#166). A Member's, because ACT-002 is
    /// the actor the capability names and a conversation configures nothing and writes nothing to
    /// the vendor — it reads the project's code and answers.
    /// </summary>
    public const string HoldConversation = "conversation.hold";

    /// <summary>
    /// Open a shell inside an executing Run's sandbox (#304), where the habitat hosts one —
    /// self-host only, by ADR-0021.
    /// <para>
    /// Distinct from <see cref="Read"/> on purpose: reading a Run observes what happened, while
    /// attaching to one executes arbitrary commands on the machine it is using. Nothing else in this
    /// list writes anything outside the product's own records.
    /// </para>
    /// <para>
    /// Granted to the Member bundle as well as the Admin one, with its cost recorded rather than
    /// hidden: a Run's sandbox carries the machine owner's own session (#288), so a Member's shell
    /// may act with the owner's credentials. Accepted deliberately on #304 — the alternative was
    /// withholding from ACT-002 the one affordance that turns a stuck Run into a fixed one — which is
    /// why every attach is recorded against the Run.
    /// </para>
    /// </summary>
    public const string Attach = "run.attach";

    /// <summary>
    /// Take a failure out of the inbox (#145). Not in ACT-002's list, and granted to Member anyway:
    /// it changes no configuration and destroys no record — the Run stays exactly as readable as it
    /// was — and an inbox only the Admin bundle could clear would stop being the shared queue UC-026
    /// describes.
    /// </summary>
    public const string DismissFailure = "run.failure.dismiss";
}
