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
/// fresh project reaches a runnable pipeline without six forms. #229 turned the same action into
/// the whole workflow in one press by asking a question first: <b>what does this repository
/// already have?</b>
/// <para>
/// The promise is <b>convergence</b>, not insertion: existing triggers are skipped by the same
/// case-insensitive identity BR-003 compares with, a uniqueness race lost to a concurrent save
/// is a skip discovered a moment later, and running the action twice creates nothing. The wiring
/// itself is catalogue content (#190's discipline) — this handler carries no methodology.
/// </para>
/// <para>
/// <b>Adoption before installation.</b> A repository whose prompts directory already holds
/// <c>grill.md</c> gets an Automation naming <i>that</i> file; a starter is written only for a
/// step with no file at all, and only for a step whose tier requires nothing. That is DEC-048's
/// reason one level up: a product-wide copy of a team's own document is the weaker of the two.
/// </para>
/// </summary>
sealed class SetUpDefaultAutomations : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations/set-up-defaults",
                async (
                    Guid projectId,
                    Request? request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(
                            projectId,
                            request?.PromptDirectory,
                            request?.InstallMissing ?? false
                        ),
                        cancellationToken
                    );
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(SetUpDefaultAutomations))
            .WithTags("Automations");

    /// <summary>
    /// Both fields are the human's confirmation, and both default to "nothing new": an absent body
    /// is exactly the #212 action, so a caller that predates discovery keeps working.
    /// <para>
    /// <paramref name="PromptDirectory"/> is a directory discovery <i>proposed</i> and a person
    /// accepted — the action never picks one itself (design D1). <paramref name="InstallMissing"/>
    /// is a second consent, because writing to somebody's repository is a different decision from
    /// creating Automations in this product.
    /// </para>
    /// </summary>
    internal sealed record Request(string? PromptDirectory = null, bool InstallMissing = false);

    /// <summary>
    /// The five facts design D5 asks for, so an Admin who did not read the repository first still
    /// learns what happened to it: where prompts are read from, what was created, what was skipped
    /// and why, what was found and left alone, and what was installed.
    /// </summary>
    internal sealed record Response(
        string Directory,
        IReadOnlyList<string> Created,
        IReadOnlyList<SkippedStep> Skipped,
        IReadOnlyList<string> FoundNotWired,
        InstalledStarters? Installed,
        IReadOnlyList<MissingPrompt> MissingPrompts
    );

    /// <summary>A trigger that already existed, and the sentence that says which case it was.</summary>
    internal sealed record SkippedStep(string Trigger, string Reason);

    /// <summary>
    /// The one pull request the gaps arrived as (design D4). <paramref name="Failure"/> is set
    /// where installing was asked for and refused — a report that omitted it would let "created
    /// five Automations" stand for "and the prompts they name exist", which is the exact confusion
    /// #190 built the missing-prompt list to prevent.
    /// </summary>
    internal sealed record InstalledStarters(
        IReadOnlyList<string> Files,
        string? PullRequestUrl,
        string? Branch,
        string? Failure
    );

    /// <summary>Where the file belongs, so the report is an instruction rather than a shrug.</summary>
    internal sealed record MissingPrompt(string SaveAs, string? ResolvedPath);

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(Guid ProjectId, string? PromptDirectory, bool InstallMissing)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Handler(
        ProjectsDbContext database,
        OverlapGuard overlaps,
        IDocumentReader documents,
        IConnectorReader connectors,
        IPromptDirectoryWriter directories,
        StarterInstaller installer
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        /// <summary>
        /// The convention, spelled here rather than taken from the Backlog module: this report
        /// names a directory before any read happens, and depending on that module's internals to
        /// borrow one string would be the larger coupling.
        /// </summary>
        const string DefaultDirectory = "ai/prompts";

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

            var directory = await ChooseDirectory(command, cancellationToken);

            // What the repository already carries, by step. A project with no Connector reads as
            // "nothing found", which is the same shape as an empty repository and lands on the
            // #212 behaviour: every step is a gap, and nothing can be installed either.
            var present = await PresentFiles(command.ProjectId, directory, cancellationToken);

            // The BR-003 identity: triggers compare case-insensitively (DEC-056), so the skip
            // decision uses the same comparison the unique index normalises with.
            var existing = await database
                .Automations.Where(automation => automation.ProjectId == command.ProjectId)
                .Select(automation => automation.TriggerLabel)
                .ToListAsync(cancellationToken);
            var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            var created = new List<string>();
            var skipped = new List<SkippedStep>();
            var missing = new List<MissingPrompt>();

            // Adopted steps first, then the installable ones the repository has no file for. A
            // step recognised from an opt-in tier is wired because its file is already there; it
            // never joins the gap list, so no button writes a methodology nobody chose.
            var adopted = PipelineSteps.All.Where(step => present.Files.ContainsKey(step.Trigger));
            var gaps = PipelineSteps
                .Installable.Where(step => !present.Files.ContainsKey(step.Trigger))
                .ToList();

            foreach (var step in adopted.Concat(gaps))
            {
                // The file the Automation names: the repository's own where there is one, the
                // starter's saved name where the gap is about to be filled.
                var promptName = present.Files.GetValueOrDefault(step.Trigger, step.Prompt.SaveAs);

                if (!taken.Add(step.Trigger))
                {
                    skipped.Add(
                        new SkippedStep(step.Trigger, "an Automation already uses this trigger")
                    );
                    continue;
                }

                var candidate = Automation.Create(
                    command.ProjectId,
                    step.Trigger,
                    triggerState: null,
                    AutomationAction.RepositoryPrompt,
                    AgentRuntime.ClaudeCodeHeadless,
                    step.Wiring.RequiresApproval,
                    CreateAutomation.DefaultTimeout,
                    promptName,
                    step.Wiring.OutputLabels
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
                    skipped.Add(new SkippedStep(step.Trigger, overlap.FirstError.Description));
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
                    skipped.Add(
                        new SkippedStep(step.Trigger, "another save created this trigger first")
                    );
                    continue;
                }

                created.Add(step.Trigger);

                // Read through the seam a Run uses, so the reported path is the path a Run would
                // resolve. Never written here: filling it is the separate consent below.
                var promptRead = await documents.ReadPrompt(
                    command.ProjectId,
                    promptName,
                    cancellationToken
                );
                if (promptRead.Content is null)
                {
                    missing.Add(new MissingPrompt(promptName, promptRead.ResolvedPath));
                }
            }

            var installed = command.InstallMissing
                ? await FillGaps(command.ProjectId, directory, gaps, cancellationToken)
                : null;

            return new Response(directory, created, skipped, present.Unmatched, installed, missing);
        }

        /// <summary>
        /// The confirmed directory is saved before anything reads it, so the Automations created
        /// here name files the resolution a Run performs will find. An unconfirmed call keeps
        /// whatever the Connector already says.
        /// </summary>
        async Task<string> ChooseDirectory(Command command, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(command.PromptDirectory))
            {
                await directories.UseDirectory(
                    command.ProjectId,
                    command.PromptDirectory,
                    cancellationToken
                );
                return command.PromptDirectory.Trim().Trim('/');
            }

            var connector = await connectors.Find(command.ProjectId, cancellationToken);
            return string.IsNullOrWhiteSpace(connector?.PromptDirectory)
                ? DefaultDirectory
                : connector.PromptDirectory;
        }

        /// <summary>
        /// The chosen directory's files, indexed by the step each one is. A file matching no step
        /// is reported and produces nothing: inventing a trigger from a filename would create a
        /// label nobody applies and an Automation that never fires (design D3).
        /// </summary>
        async Task<(
            Dictionary<string, string> Files,
            IReadOnlyList<string> Unmatched
        )> PresentFiles(Guid projectId, string directory, CancellationToken cancellationToken)
        {
            var listing = await documents.ListPromptFiles(projectId, directory, cancellationToken);

            var byStep = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new List<string>();

            foreach (var file in listing.Files)
            {
                var step = PipelineSteps.Match(file);

                // First file wins a step, so a directory holding both grill.md and aio-grill.md
                // wires one Automation and reports the loser as found rather than picking twice.
                if (step is null || !byStep.TryAdd(step.Trigger, file))
                {
                    unmatched.Add(file);
                }
            }

            return (byStep, unmatched);
        }

        /// <summary>
        /// Every gap in one branch and one draft pull request (design D4). #214 opens one per
        /// starter, which is right when a human picks one; four gaps picked by one press are one
        /// decision, and four reviews of one decision is the cost this removes.
        /// </summary>
        async Task<InstalledStarters> FillGaps(
            Guid projectId,
            string directory,
            IReadOnlyList<PipelineStep> gaps,
            CancellationToken cancellationToken
        )
        {
            if (gaps.Count == 0)
            {
                return new InstalledStarters([], null, null, Failure: null);
            }

            var files = gaps.Select(step => $"{directory}/{step.Prompt.SaveAs}").ToList();
            const string branch = "starter/pipeline";

            var published = await installer.Install(
                projectId,
                branch,
                [
                    .. gaps.Select(
                        (step, index) =>
                            new StarterInstaller.File(files[index], step.Prompt.Content)
                    ),
                ],
                "docs(prompts): install the starter prompts this pipeline needs",
                "Installs the starter prompts for the pipeline steps this repository had no file "
                    + $"for, under `{directory}/`. Installed from the portal (#229); review and "
                    + "merge to make them available to the Automations already created.",
                cancellationToken
            );

            return published.IsError
                ? new InstalledStarters([], null, null, published.FirstError.Description)
                : new InstalledStarters(files, published.Value, branch, Failure: null);
        }
    }
}
