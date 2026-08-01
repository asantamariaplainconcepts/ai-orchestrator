using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// #212 — UC-005 in bulk: the starter catalogue's wired Automations created in one action, so a
/// fresh project reaches a runnable pipeline without six forms.
/// <para>
/// The promise is <b>convergence</b>, not insertion: existing triggers are skipped by the same
/// case-insensitive identity BR-003 compares with, a uniqueness race lost to a concurrent save
/// is a skip discovered a moment later, and running the action twice creates nothing. The wiring
/// itself is catalogue content (#190's discipline) — this handler carries no methodology.
/// </para>
/// <para>
/// <b>Nothing here writes to the repository.</b> The prompt files the created Automations name
/// are read through the same seam a Run uses, and the absent ones are reported with where they
/// belong — copying them in stays the human's act (#190, design D1).
/// </para>
/// </summary>
sealed class SetUpDefaultAutomations : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations/set-up-defaults",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(projectId), cancellationToken);
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(SetUpDefaultAutomations))
            .WithTags("Automations");

    /// <summary>
    /// Three lists, so the Admin knows exactly what happened and what is still theirs to do:
    /// what was created, what already existed (by trigger), and the prompt paths the created
    /// Automations name that the repository does not contain yet.
    /// </summary>
    internal sealed record Response(
        IReadOnlyList<string> Created,
        IReadOnlyList<string> Skipped,
        IReadOnlyList<MissingPrompt> MissingPrompts
    );

    /// <summary>Where the file belongs, so the report is an instruction rather than a shrug.</summary>
    internal sealed record MissingPrompt(string SaveAs, string? ResolvedPath);

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(Guid ProjectId) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Handler(
        ProjectsDbContext database,
        OverlapGuard overlaps,
        IDocumentReader documents
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var projectExists = await database.Projects.AnyAsync(
                project => project.Id == command.ProjectId,
                cancellationToken
            );
            if (!projectExists)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            // The BR-003 identity: triggers compare case-insensitively (DEC-056), so the skip
            // decision uses the same comparison the unique index normalises with.
            var existing = await database
                .Automations.Where(automation => automation.ProjectId == command.ProjectId)
                .Select(automation => automation.TriggerLabel)
                .ToListAsync(cancellationToken);
            var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            var created = new List<string>();
            var skipped = new List<string>();
            var missing = new List<MissingPrompt>();

            var wired = StarterCatalogue
                .Tiers.SelectMany(tier => tier.Prompts)
                .Where(prompt => prompt.Automation is not null);

            foreach (var prompt in wired)
            {
                var wiring = prompt.Automation!;

                if (!taken.Add(wiring.Trigger))
                {
                    skipped.Add(wiring.Trigger);
                    continue;
                }

                var candidate = Automation.Create(
                    command.ProjectId,
                    wiring.Trigger,
                    triggerState: null,
                    AutomationAction.RepositoryPrompt,
                    AgentRuntime.ClaudeCodeHeadless,
                    wiring.RequiresApproval,
                    CreateAutomation.DefaultTimeout,
                    prompt.SaveAs,
                    wiring.OutputLabels
                );

                // Subsumption against what the project already has (a state-scoped trigger the
                // set comparison above cannot see) converges to a skip: the action's promise is
                // that the set exists, not that this call inserted it.
                var overlap = await overlaps.Check(
                    candidate,
                    command.ProjectId,
                    excluding: null,
                    cancellationToken
                );
                if (overlap.IsError)
                {
                    skipped.Add(wiring.Trigger);
                    continue;
                }

                database.Automations.Add(candidate);

                try
                {
                    await database.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception)
                    when (OverlapGuard.IsDuplicateTrigger(exception))
                {
                    // A concurrent save won the insert (the CreateAutomation race, converged
                    // instead of surfaced): drop the loser from the change tracker and move on.
                    database.Entry(candidate).State = EntityState.Detached;
                    skipped.Add(wiring.Trigger);
                    continue;
                }

                created.Add(wiring.Trigger);

                // Read through the seam a Run uses, so the reported path is the path a Run
                // would resolve. Never written: the absence is the Admin's instruction.
                var promptRead = await documents.ReadPrompt(
                    command.ProjectId,
                    prompt.SaveAs,
                    cancellationToken
                );
                if (promptRead.Content is null)
                {
                    missing.Add(new MissingPrompt(prompt.SaveAs, promptRead.ResolvedPath));
                }
            }

            return new Response(created, skipped, missing);
        }
    }
}
