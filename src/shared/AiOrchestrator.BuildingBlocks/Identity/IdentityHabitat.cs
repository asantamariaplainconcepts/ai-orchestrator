using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// The one question a module is allowed to ask about identity: does this habitat have people who
/// sign in, and therefore roles that are rows rather than a foregone conclusion?
/// <para>
/// It lives here, below the composition, because two things need the same answer and must not be
/// able to disagree: the host, which composes the principal, and the Projects module, which owns
/// the role table and has to know whether an empty one means "nobody may administer anything" or
/// "there is one person and the machine is theirs".
/// </para>
/// <para>
/// Presence of configuration, never an environment name — the reason is recorded at the composition
/// itself: DEC-049's self-host compose defaults to Production, and gating on that once refused to
/// start the very habitat it protects.
/// </para>
/// </summary>
public static class IdentityHabitat
{
    /// <summary>The provider's client id. Its presence is what says "people sign in here".</summary>
    public const string ProviderClientIdKey = "AzureAd:ClientId";

    public static bool CallersSignIn(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[ProviderClientIdKey]);

    /// <summary>Set to <c>LocalOwner</c> on a machine one person owns. Absent everywhere else.</summary>
    public const string ModeKey = "Identity:Mode";

    /// <summary>
    /// The second habitat question (#210), asked the same way and for the same reason as
    /// <see cref="CallersSignIn"/>: the host composes the LocalOwner principal from this value,
    /// and the Backlog module has to know whether a folder on this machine is something an Admin
    /// may name as a code source. Both read one key, so they cannot disagree — a deployment
    /// whose identity is not the local owner has no business reading the host's disk.
    /// </summary>
    public static bool IsSelfHost(IConfiguration configuration) =>
        string.Equals(configuration[ModeKey], "LocalOwner", StringComparison.OrdinalIgnoreCase);
}
