namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// Who is asking — never <i>whether</i> anybody is asking (#119, design D1).
/// <para>
/// There is no null case and no "is identity configured" flag, deliberately. The moment such a
/// flag exists every call site acquires a second path, and in a hosted deployment it is the path
/// nobody exercises. Consumers ask for the principal and get one; which one is the host's
/// decision, exactly as it is for <c>ISecretResolver</c>.
/// </para>
/// <para>
/// It answers <i>who</i> and nothing else. What they may do is <see cref="IProjectPermissions"/>,
/// because BR-009's roles are per project and "this caller's role" has no answer without naming
/// one (#13, design D2).
/// </para>
/// </summary>
public interface ICurrentPrincipal
{
    /// <summary>The caller, as this habitat knows them.</summary>
    Principal Current { get; }
}

/// <summary>
/// A caller. <paramref name="Id"/> is stable for the same person in the same habitat, so it can
/// be recorded; <paramref name="DisplayName"/> is what a human reads.
/// </summary>
public sealed record Principal(string Id, string DisplayName)
{
    /// <summary>The machine's owner, who never signs in because the machine is theirs.</summary>
    public const string LocalOwnerId = "local-owner";

    /// <summary>
    /// Nobody: either the habitat has no provider (DEC-049's self-host, which warns at startup),
    /// or this is the window before a challenge completes in a habitat that does have one.
    /// <para>
    /// Those two are not the same thing, and nothing may treat them as one. In the first, the sole
    /// caller holds everything; in the second, they hold nothing yet. Which it is depends on the
    /// habitat and never on this id — see <see cref="IdentityHabitat"/>. An earlier draft of #13
    /// inferred it from the id here, which would have handed Admin to an unauthenticated caller in
    /// the deployed portal had the pipeline's 401 ever been carved out for a route that dispatches.
    /// </para>
    /// </summary>
    public const string AnonymousId = "anonymous";
}
