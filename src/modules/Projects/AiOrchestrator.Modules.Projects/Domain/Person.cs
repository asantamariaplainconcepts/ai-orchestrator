using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// Somebody who has signed in at least once (#13, task 4.1). Not a users table and not an
/// account: nothing here is authoritative, and nothing here authenticates anybody. It exists for
/// one reason — a role is granted to an identity, and an Admin cannot type a provider object id
/// from memory.
/// <para>
/// So the grant surface offers the people this deployment has actually seen, and granting to
/// somebody it has not seen is refused rather than accepted-and-inert. That refusal is the honest
/// face of design D6: an invitation to somebody who has never signed in is a different feature,
/// and pretending to support it by storing a row against a name would be the mailbox-following
/// mistake D3 rejects.
/// </para>
/// <para>
/// The display name is refreshed each time they are seen, because it is the provider's to change
/// and a stale one in a role list is how an Admin grants to the wrong person.
/// </para>
/// </summary>
sealed class Person : Aggregate
{
    Person() { }

    Person(string identityId, string displayName, DateTimeOffset at)
    {
        IdentityId = identityId;
        DisplayName = displayName;
        FirstSeenAt = at;
        LastSeenAt = at;
    }

    public string IdentityId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public static Person FirstSeen(string identityId, string displayName, DateTimeOffset at) =>
        new(identityId, displayName, at);

    public void SeenAgain(string displayName, DateTimeOffset at)
    {
        DisplayName = displayName;
        LastSeenAt = at;
    }
}
