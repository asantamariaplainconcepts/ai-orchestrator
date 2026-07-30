using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Identity.UseCases;

/// <summary>
/// UC-002 — who holds what on this Project, and who could (#13, task 4.2).
/// <para>
/// Admin rather than Member: the roster is the assignment surface, and the candidate list is
/// everybody this deployment has ever seen. A Member has no use for either and every reason not to
/// receive the second.
/// </para>
/// <para>
/// Candidates come from the people table rather than from the provider's directory. That is the
/// honest shape of design D6: this deployment can only offer somebody it has actually met, and a
/// directory search would let an Admin grant to an identity that has never been here — a row that
/// looks granted and does nothing.
/// </para>
/// </summary>
sealed class ListProjectRoles : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/roles",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(ListProjectRoles))
            .WithTags("Identity");

    [Requires(ProjectPermissions.ManageRoles)]
    internal sealed record Query(Guid ProjectId) : IQuery<Response>, IScopedToProject;

    internal sealed record Holder(
        string IdentityId,
        string DisplayName,
        string Role,
        DateTimeOffset GrantedAt
    );

    internal sealed record Candidate(string IdentityId, string DisplayName);

    /// <summary>
    /// <paramref name="Bundles"/> is the vocabulary the form offers, from the enum rather than from
    /// a list in the UI — DEC-034 fixes it at two, and a hard-coded pair in a form is how a third
    /// would arrive in one place and not the other.
    /// </summary>
    internal sealed record Response(
        IReadOnlyList<Holder> Holders,
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<string> Bundles
    );

    internal sealed class Handler(ProjectsDbContext database) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var assignments = await database
                .ProjectRoles.Where(row => row.ProjectId == query.ProjectId)
                .ToListAsync(cancellationToken);

            var people = await database.People.ToListAsync(cancellationToken);
            var names = people.ToDictionary(
                person => person.IdentityId,
                person => person.DisplayName
            );

            // The role survives a person the deployment has somehow forgotten: the row is keyed by
            // identity, not by the people table, so a missing name is a display problem and never a
            // silently dropped permission. Showing the id is what tells an Admin that.
            var holders = assignments
                .Select(row => new Holder(
                    row.IdentityId,
                    names.TryGetValue(row.IdentityId, out var name) ? name : row.IdentityId,
                    row.Role.ToString(),
                    row.GrantedAt
                ))
                .OrderBy(holder => holder.DisplayName)
                .ToList();

            var held = assignments.Select(row => row.IdentityId).ToHashSet();
            var candidates = people
                .Where(person => !held.Contains(person.IdentityId))
                .OrderBy(person => person.DisplayName)
                .Select(person => new Candidate(person.IdentityId, person.DisplayName))
                .ToList();

            return new Response(holders, candidates, Enum.GetNames<ProjectRole>());
        }
    }
}
