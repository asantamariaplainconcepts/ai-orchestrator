using AiOrchestrator.BuildingBlocks.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// A unit of configuration: one Connector, its Automations, its caps (BC-001).
/// Only the name exists at this stage — Connector and Automation arrive as product changes.
/// </summary>
sealed class Project : Aggregate
{
    Project() { }

    Project(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// When this Project was retired, or null while it is live (#121, design D3). A timestamp
    /// rather than a flag because the list wants to say <i>when</i>, and a boolean would need a
    /// second column the moment anybody asked.
    /// <para>
    /// Archiving stops new work — no polling, no matching, no manual Run — and stops nothing
    /// else: what its agents already did stays readable, because BR-014 makes that record the
    /// audit trail rather than clutter.
    /// </para>
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>
    /// The runtime an Automation with no explicit one resolves to at execution time
    /// (project-runtimes). Null means the deployment default — absence is an answer here, the
    /// same way an unset Automation runtime is.
    /// </summary>
    public string? DefaultRuntime { get; private set; }

    /// <summary>
    /// Credential secret <b>names</b> per runtime (BR-010: names stored, values never). The
    /// project's billing identity where one exists; the deployment's config supplies the
    /// fallback.
    /// </summary>
    public List<ProjectRuntimeCredential> RuntimeCredentials { get; private set; } = [];

    /// <summary>
    /// The project's Story lifecycle: stage names in the order a Story travels them (#310, design
    /// D1). <b>Array position is the order</b>, so rearranging is one write of one value rather
    /// than a renumbering that can leave gaps or duplicates mid-transaction.
    /// <para>
    /// Stored rather than derived, which supersedes one clause of DEC-053 (ADR-0022): an order a
    /// person can rearrange has nowhere else to live. Deriving the shape from labels answered
    /// neither of #310's questions — in a derived graph the first step's trigger <i>is</i> the entry
    /// point, so there is no "before" to place anything into, and there is no stored order to
    /// change.
    /// </para>
    /// <para>
    /// A stage exists only as a consequence of an Automation claiming a transition that names it,
    /// and is never pruned: what removing a stage would mean for Stories already carrying its label
    /// is a decision nobody has made, and inventing one here would be worse than accumulating.
    /// </para>
    /// <para>
    /// Uniqueness is the operations' guarantee, not the setter's — nothing adds a stage except the
    /// operation that claims a transition, and that resolves case-insensitively first.
    /// Re-normalising on load would quietly rewrite history, the same reason
    /// <c>Automation.OutputLabels</c> gives.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> LifecycleStages
    {
        get => _lifecycleStages;
        private set => _lifecycleStages = [.. value];
    }

    // EF materialises into the backing field, exactly as Automation's label set does.
    List<string> _lifecycleStages = [];

    /// <summary>
    /// The stage as it is <i>stored</i>, matched the way the vendor matches labels (DEC-056), or
    /// null when this project has no such stage. Returns the stored spelling rather than the one
    /// asked for, so a claim naming <c>Ai:Propose</c> against a stored <c>ai:propose</c> uses the
    /// stage that already exists instead of creating a second spelling of it.
    /// <para>
    /// Deliberately <see cref="Automation.SameLabel"/> and not a second comparison: the guard that
    /// accepted a differently-cased trigger the matcher would never fire is exactly what one shared
    /// comparison prevents (#147, design D4).
    /// </para>
    /// </summary>
    public string? ResolveStage(string? name) =>
        IndexOfStage(name) is var at and >= 0 ? _lifecycleStages[at] : null;

    /// <summary>
    /// Inserts a stage immediately before another, leaving the order of every existing stage
    /// unchanged (AC 4 — "and the order of the existing stages is unchanged"). False when
    /// <paramref name="before"/> is not a stage of this project, or when the stage being inserted
    /// is already one.
    /// </summary>
    public bool InsertStageBefore(string stage, string before)
    {
        if (ResolveStage(stage) is not null)
        {
            return false;
        }

        var at = IndexOfStage(before);
        if (at < 0)
        {
            return false;
        }

        _lifecycleStages.Insert(at, stage.Trim());
        return true;
    }

    /// <summary>
    /// Adds a stage at the end. Beyond the three operations #310's plan named, and here because the
    /// other direction of the same act needs it: an Automation claiming a transition <i>out of</i>
    /// the last stage into a new one extends the flow, which is how a starter tier installed
    /// claim-by-claim gets a lifecycle at all (design D10). False when it is already a stage.
    /// </summary>
    public bool AppendStage(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage) || ResolveStage(stage) is not null)
        {
            return false;
        }

        _lifecycleStages.Add(stage.Trim());
        return true;
    }

    /// <summary>
    /// The one place the adjacency invariant lives (#310, design D4): <b>a claim names two adjacent
    /// stages of this project's lifecycle</b>, and the operation that claims is what keeps that
    /// true — not a nightly repair, and not a second copy of the rule in whichever slice writes
    /// next. A rule implemented twice eventually disagrees with itself, which is the lesson
    /// <c>OverlapGuard</c> records for BR-003.
    /// <para>
    /// Storing both the order and each claim is what makes disagreement possible at all: an
    /// Automation could otherwise claim <c>s1 → s3</c> while the list says <c>s1, s2, s3</c>. So a
    /// claim either finds its boundary already there, or creates exactly the stage it needs:
    /// </para>
    /// <list type="bullet">
    /// <item>the boundary already exists — the claim is stored and the list is untouched (AC 5);</item>
    /// <item>the from-stage is not yet a stage — it is inserted <i>immediately before</i> the
    /// to-stage, and the order of every existing stage is unchanged (AC 4, which is how a step gets
    /// placed first);</item>
    /// <item>the to-stage is not yet a stage — it is inserted immediately after the from-stage,
    /// which is how a flow is extended at its end (design D10's starter tiers);</item>
    /// <item>neither is a stage — both are appended, in order, which is how a stageless project
    /// acquires a lifecycle without "seed a default lifecycle" coming into scope;</item>
    /// <item>both are stages but not adjacent — refused, and the lifecycle is unchanged.</item>
    /// </list>
    /// <para>
    /// Case is folded throughout, so a claim naming a stage that differs only in spelling uses the
    /// stage that exists rather than creating a second one (DEC-056).
    /// </para>
    /// <para>
    /// <b>Known gap, stated rather than guessed at.</b> Inserting a stage between two existing ones
    /// can leave a <i>third</i> Automation's already-stored claim non-adjacent. #310's plan asks
    /// this operation for the three cases above and no more, and enforcing sibling adjacency here
    /// would need the siblings' claims — so the hole is recorded for the reviewer instead of being
    /// closed by invention. BR-003 keeps it narrow in practice: at most one enabled Automation
    /// claims a transition out of any one from-stage.
    /// </para>
    /// </summary>
    public ErrorOr<Success> ClaimTransition(string? fromStage, string? toStage)
    {
        // Null or blank is "claims no transition" (design D3) — a no-op rather than a refusal, so
        // DEC-053's standalone Automation and the last stage of a lifecycle both stay expressible.
        if (string.IsNullOrWhiteSpace(fromStage) || string.IsNullOrWhiteSpace(toStage))
        {
            return Result.Success;
        }

        var from = IndexOfStage(fromStage);
        var to = IndexOfStage(toStage);

        if (from >= 0 && to >= 0)
        {
            return to == from + 1
                ? Result.Success
                : ProjectErrors.StagesNotAdjacent(
                    _lifecycleStages[from],
                    _lifecycleStages[to],
                    _lifecycleStages
                );
        }

        if (to >= 0)
        {
            InsertStageBefore(fromStage, _lifecycleStages[to]);
            return Result.Success;
        }

        if (from >= 0)
        {
            // Immediately after the from-stage, which is "before whatever followed it" — or the end,
            // when the from-stage was the last one.
            if (from + 1 < _lifecycleStages.Count)
            {
                InsertStageBefore(toStage, _lifecycleStages[from + 1]);
            }
            else
            {
                AppendStage(toStage);
            }

            return Result.Success;
        }

        AppendStage(fromStage);
        AppendStage(toStage);
        return Result.Success;
    }

    int IndexOfStage(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? -1
            : _lifecycleStages.FindIndex(stage => Automation.SameLabel(stage, name));

    /// <summary>
    /// Full replace, like the Automation update: the form always shows every field, so a field
    /// it omitted would silently reset — the same reasoning #151 recorded.
    /// </summary>
    public void ConfigureRuntimes(
        string? defaultRuntime,
        IReadOnlyDictionary<string, string> credentialNames
    )
    {
        DefaultRuntime = string.IsNullOrWhiteSpace(defaultRuntime) ? null : defaultRuntime.Trim();
        RuntimeCredentials.Clear();
        foreach (var (runtime, secretName) in credentialNames)
        {
            if (!string.IsNullOrWhiteSpace(secretName))
            {
                RuntimeCredentials.Add(new ProjectRuntimeCredential(runtime, secretName.Trim()));
            }
        }
    }

    public static Project Create(string name) => new(name);

    /// <summary>Idempotent: archiving an archived Project keeps the original moment.</summary>
    public void Archive(DateTimeOffset at) => ArchivedAt ??= at;

    public void Restore() => ArchivedAt = null;
}

/// <summary>One runtime's credential name on a Project — a name, never a value (BR-010).</summary>
sealed class ProjectRuntimeCredential
{
    ProjectRuntimeCredential() { }

    public ProjectRuntimeCredential(string runtime, string secretName)
    {
        Runtime = runtime;
        SecretName = secretName;
    }

    public Guid Id { get; private set; }

    public string Runtime { get; private set; } = string.Empty;

    public string SecretName { get; private set; } = string.Empty;
}
