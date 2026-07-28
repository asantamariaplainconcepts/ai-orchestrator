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
/// The shape is chosen for the implementation that does not exist yet: when an identity provider
/// arrives it replaces one implementation of this seam and touches nothing above it.
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
public sealed record Principal(string Id, string DisplayName, PrincipalRole Role);

/// <summary>
/// BR-009's bundles, as DEC-034 fixed them: Admin is everything, Member observes and triggers.
/// The rule stays unimplemented until operations name their permissions — this is what those
/// checks will have to check against.
/// </summary>
public enum PrincipalRole
{
    Member = 1,
    Admin = 2,
}
