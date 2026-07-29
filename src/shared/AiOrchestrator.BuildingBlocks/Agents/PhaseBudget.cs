namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What a single Agent phase is allowed to cost (BR-005, bounded by DEC-054 in #144).
/// <para>
/// Here rather than in the Projects module because the contract spans modules: Projects refuses a
/// value above the ceiling, the dispatch worker refuses to *start* a phase it cannot fit in its
/// remaining platform budget, and neither may depend on the other's internals.
/// </para>
/// <para>
/// <b>Three sites, one contract.</b> A test cannot span a C# constant, a Terraform value and a
/// written rule, so each of the three names the other two:
/// </para>
/// <list type="bullet">
///   <item><description>this constant — the ceiling an Admin cannot exceed;</description></item>
///   <item><description><c>replica_timeout_in_seconds</c> in <c>infra/dev/dispatch.tf</c>, which
///   must be at least this plus a drain margin;</description></item>
///   <item><description>BR-005 in <c>docs/product/mvp/05-business-rules.md</c>, the rule both
///   serve.</description></item>
/// </list>
/// </summary>
public static class PhaseBudget
{
    /// <summary>
    /// The ceiling. Bounded, and the bound is what makes BR-005's promise keepable: a phase runs
    /// inside a platform budget, and with no upper limit here there is no value that budget could be
    /// set to that is provably enough — "Admin-configurable" would mean "configurable up to whatever
    /// the infrastructure happens to allow".
    /// </summary>
    public const int MaximumMinutes = 60;

    /// <summary>BR-005's default, applied when an Automation names no timeout of its own.</summary>
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(30);

    /// <summary>The ceiling as a span, for callers reasoning about time rather than validating.</summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(MaximumMinutes);
}
