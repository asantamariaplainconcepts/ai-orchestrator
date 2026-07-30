using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Identity;

/// <summary>
/// Records that a person exists (#13, task 4.1), in the one place that does it.
/// <para>
/// The invariant it protects: <b>anybody holding a role is somebody this deployment has met</b>. Two
/// paths create that obligation — signing in, and creating a Project, which grants its creator Admin
/// — and when only the first recorded, the creator held Admin while the grant surface refused to
/// manage them and the roster showed their raw object id. Found by a test that tried to demote the
/// only administrator and was told the person did not exist.
/// </para>
/// <para>
/// Deliberately not a users table: nothing here is authoritative and nothing here authenticates
/// anybody. It exists so an Admin can pick a name instead of typing a provider object id, and so
/// granting to somebody who has never been here can be refused rather than accepted and inert.
/// </para>
/// </summary>
sealed class KnownPeople(ProjectsDbContext database, TimeProvider clock)
{
    /// <summary>
    /// Notes the caller, unless they are one of the habitats with a single occupant: their ids are
    /// composed rather than issued, and storing one as a grantable person would offer
    /// "local-owner" in a picker the day that deployment gained a provider.
    /// <para>
    /// Does not save — the caller does, so this can ride in the same transaction as whatever made
    /// the person matter.
    /// </para>
    /// </summary>
    public async Task Note(Principal caller, CancellationToken cancellationToken)
    {
        if (caller.Id is Principal.LocalOwnerId or Principal.AnonymousId)
        {
            return;
        }

        var known = await database.People.FirstOrDefaultAsync(
            person => person.IdentityId == caller.Id,
            cancellationToken
        );

        if (known is not null)
        {
            // Refreshed every time, because the display name is the provider's to change and a stale
            // one in a role list is how an Admin grants to the wrong person.
            known.SeenAgain(caller.DisplayName, clock.GetUtcNow());
            return;
        }

        database.People.Add(Person.FirstSeen(caller.Id, caller.DisplayName, clock.GetUtcNow()));
    }
}
