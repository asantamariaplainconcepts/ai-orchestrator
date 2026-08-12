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
                            request?.InstallMissing ?? false,
                            request?.Steps,
                            request?.Tiers
                        ),
                        cancellationToken
                    );
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(SetUpDefaultAutomations))
            .WithTags("Automations");

    /// <summary>
    /// The human's confirmation, defaulting to "nothing new": an absent body is exactly the #212
    /// action, so a caller that predates discovery keeps working.
    /// <para>
    /// <paramref name="PromptDirectory"/> is a directory discovery <i>proposed</i> and a person
    /// accepted — the action never picks one itself (design D1). <paramref name="InstallMissing"/>
    /// is a second consent, because writing to somebody's repository is a different decision from
    /// creating Automations in this product.
    /// </para>
    /// <para>
    /// <paramref name="Steps"/> is which steps the Admin kept (#262), and <b>absent is not
    /// empty</b>: <c>null</c> means every step — so a caller that sends no selection, or no body at
    /// all, behaves exactly as it did before this field existed — while <c>[]</c> means none, a
    /// lawful no-op that creates nothing and reports every step excluded. The two differ by a pull
    /// request, which is why the distinction is written down rather than left to the reader.
    /// </para>
    /// </summary>
    /// <param name="Tiers">
    /// Which starter tiers the Admin consented to install (#269), by catalogue id. <b>Absent means no
    /// tier</b> — the opposite default from <paramref name="Steps"/> on this same record, and the
    /// asymmetry is deliberate rather than an oversight.
    /// <para>
    /// A selection <i>narrows</i> a plan the caller was already shown, so "everything you proposed" is
    /// the safe default. A consent <i>authorises writing files into somebody's repository</i>, so
    /// "nothing" is the safe default. Aligning the two for symmetry would make the safer field the
    /// more dangerous one: a caller that forgot to send a consent would install a methodology.
    /// </para>
    /// <para>
    /// Compared exactly (see <see cref="PipelineSteps.Installable"/>) — these are ids echoed back from
    /// discovery, not labels a human types. A name the catalogue does not contain matches nothing.
    /// </para>
    /// </param>
    internal sealed record Request(
        string? PromptDirectory = null,
        bool InstallMissing = false,
        IReadOnlyList<string>? Steps = null,
        IReadOnlyList<string>? Tiers = null
    );

    /// <summary>
    /// The five facts design D5 asks for, so an Admin who did not read the repository first still
    /// learns what happened to it: where prompts are read from, what was created, what was skipped
    /// and why, what was found and left alone, and what was installed.
    /// <para>
    /// <paramref name="Excluded"/> is the sixth, and deliberately not a <see cref="SkippedStep"/>
    /// reason (#262): "skipped" answers <i>was this already set up?</i>, and folding the Admin's own
    /// choice into that count would make one number mean two things.
    /// </para>
    /// </summary>
    internal sealed record Response(
        string Directory,
        IReadOnlyList<string> Created,
        IReadOnlyList<SkippedStep> Skipped,
        IReadOnlyList<string> FoundNotWired,
        InstalledStarters? Installed,
        IReadOnlyList<MissingPrompt> MissingPrompts,
        IReadOnlyList<string> Excluded
    );

    /// <summary>A trigger that already existed, and the sentence that says which case it was.</summary>
    internal sealed record SkippedStep(string Trigger, string Reason);

    /// <summary>
    /// The one pull request the gaps arrived as (design D4). <paramref name="Failure"/> is set
    /// where installing was asked for and refused — a report that omitted it would let "created
    /// five Automations" stand for "and the prompts they name exist", which is the exact confusion
    /// #190 built the missing-prompt list to prevent.
    /// </summary>
    /// <param name="Prerequisites">
    /// The files written outside the prompt directory (#269) — an OpenSpec layout, process documents —
    /// kept apart from <paramref name="Files"/> rather than folded in. An Admin who consented to
    /// prompts has to be able to see, without opening the diff, that their repository's process
    /// documents were touched; one list covering both would let a count of prompts stand for that.
    /// </param>
    /// <param name="PrerequisitesAlreadyPresent">
    /// Prerequisite paths left exactly as they were, because the repository already had them. Reported
    /// because "we wrote four of seven" is only legible beside which three were yours already.
    /// </param>
    internal sealed record InstalledStarters(
        IReadOnlyList<string> Files,
        string? PullRequestUrl,
        string? Branch,
        string? Failure,
        IReadOnlyList<string> Prerequisites,
        IReadOnlyList<string> PrerequisitesAlreadyPresent
    );

    /// <summary>Where the file belongs, so the report is an instruction rather than a shrug.</summary>
    internal sealed record MissingPrompt(string SaveAs, string? ResolvedPath);

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(
        Guid ProjectId,
        string? PromptDirectory,
        bool InstallMissing,
        IReadOnlyList<string>? Steps = null,
        IReadOnlyList<string>? Tiers = null
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

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
            // Loaded rather than merely counted since #310: each wired step claims a transition, and a
            // claim creates the stages it names — so the Project is part of this write and not only a
            // precondition for it. This is how installing a tier gives a new project a lifecycle
            // (design D10), which is why "seed a default lifecycle" stays out of scope.
            var project = await database.Projects.FirstOrDefaultAsync(
                entity => entity.Id == command.ProjectId,
                cancellationToken
            );
            if (project is null)
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
            var adopted = PipelineSteps
                .All.Where(step => present.Files.ContainsKey(step.Trigger))
                .ToList();
            // Only the tiers this caller consented to can contribute a gap (#269). With no consent
            // and no ungated tier in the catalogue this list is empty, which is what makes an
            // unconsented press wire what is there and write nothing.
            var gaps = PipelineSteps
                .Installable(command.Tiers)
                .Where(step => !present.Files.ContainsKey(step.Trigger))
                .ToList();

            // The Admin's selection (#262), compared with the same case-insensitive identity
            // BR-003 uses. Absent means every step — an unselected caller keeps its old behaviour;
            // an empty selection means none, and the two are not the same answer.
            var selection = command.Steps is null
                ? null
                : new HashSet<string>(command.Steps, StringComparer.OrdinalIgnoreCase);

            bool Selected(PipelineStep step) =>
                selection is null || selection.Contains(step.Trigger);

            // Filtered here, ahead of the loop, so an excluded step never reaches the already-exists
            // and overlap checks below: it belongs in exactly one list, and it is this one. A
            // selected trigger naming a step this invocation would not have acted on matches
            // nothing — no error, and no work the action never proposed.
            var excluded = adopted
                .Concat(gaps)
                .Where(step => !Selected(step))
                .Select(step => step.Trigger)
                .ToList();

            foreach (var step in adopted.Concat(gaps).Where(Selected))
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
                    CreateAutomation.DefaultTimeout,
                    promptName,
                    step.Wiring.Marks,
                    previewPort: null,
                    model: null,
                    toStage: step.Wiring.ToStage
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

                // The claim and the stages it creates are one write, exactly as the create endpoint does
                // it — and BR-003 was asked above, so a refused step leaves the lifecycle untouched
                // even in memory. A claim the adjacency guard refuses is a skip with its own reason
                // rather than a half-installed tier.
                var claim = project.ClaimTransition(candidate.TriggerLabel, candidate.ToStage);
                if (claim.IsError)
                {
                    skipped.Add(new SkippedStep(step.Trigger, claim.FirstError.Description));
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

            // Only the gaps still selected reach the installer. That is also what keeps the
            // no-pull-request promise honest: FillGaps already short-circuits on an empty list, so
            // "the Admin excluded every gap" and "the repository already had every file" converge
            // on one path. Handing an empty list further down would earn a Workspace.NoChanges
            // refusal, and reporting a failure for a choice somebody made is the wrong answer.
            // A tier's documents follow a tier that is actually being acted on. Consent alone is not
            // enough: an Admin who consented and then unchecked every row has said "create nothing",
            // and writing seven process documents into their repository at that point would be the
            // press ignoring the checklist it just showed them. Conversely a tier whose prompts all
            // already exist *is* being acted on — those rows are selected and being wired — so its
            // documents still arrive, which is the whole point of consenting on an adopted pipeline.
            var actedOnTiers = adopted
                .Concat(gaps)
                .Where(Selected)
                .Select(step => step.Tier.Id)
                .ToHashSet(StringComparer.Ordinal);

            var installed = command.InstallMissing
                ? await FillGaps(
                    command.ProjectId,
                    directory,
                    [.. gaps.Where(Selected)],
                    Prerequisites(command.Tiers?.Where(actedOnTiers.Contains).ToList()),
                    cancellationToken
                )
                : null;

            return new Response(
                directory,
                created,
                skipped,
                present.Unmatched,
                installed,
                missing,
                excluded
            );
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
        /// The prerequisite files of every tier this caller consented to (#269). Distinct paths only:
        /// two tiers naming the same path would otherwise write it twice, and the first write would
        /// make the second's absence check lie.
        /// </summary>
        static IReadOnlyList<StarterPrerequisite> Prerequisites(IReadOnlyList<string>? consented) =>
            consented is null or []
                ? []
                :
                [
                    .. StarterCatalogue
                        .Tiers.Where(tier => consented.Contains(tier.Id, StringComparer.Ordinal))
                        .SelectMany(tier => tier.Prerequisites)
                        .DistinctBy(prerequisite => prerequisite.Path, StringComparer.Ordinal),
                ];

        /// <summary>
        /// Every gap in one branch and one draft pull request (design D4). #214 opens one per
        /// starter, which is right when a human picks one; four gaps picked by one press are one
        /// decision, and four reviews of one decision is the cost this removes.
        /// <para>
        /// Since #269 the same branch also carries the consented tiers' prerequisites — the documents
        /// its prompts read. One press is one decision, and a workflow whose prompts and whose
        /// documents arrived as two reviews could be merged half-way, which is precisely the state the
        /// prerequisites exist to prevent.
        /// </para>
        /// </summary>
        async Task<InstalledStarters> FillGaps(
            Guid projectId,
            string directory,
            IReadOnlyList<PipelineStep> gaps,
            IReadOnlyList<StarterPrerequisite> prerequisites,
            CancellationToken cancellationToken
        )
        {
            // Nothing selected and nothing consented: no branch, no pull request, no failure. A
            // consented tier whose prompts all already exist still reaches the installer, because its
            // documents may not — that is the case this guard used to swallow.
            if (gaps.Count == 0 && prerequisites.Count == 0)
            {
                return new InstalledStarters([], null, null, Failure: null, [], []);
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
                    // OnlyIfAbsent: unlike a prompt gap, nothing upstream has established that these
                    // paths are free — an existing file always wins, decided against the clone.
                    .. prerequisites.Select(prerequisite => new StarterInstaller.File(
                        prerequisite.Path,
                        prerequisite.Content,
                        OnlyIfAbsent: true
                    )),
                ],
                "docs(prompts): install the starter prompts this pipeline needs",
                $"Installs the starter prompts for the pipeline steps this repository had no file for, under `{directory}/`"
                    + (
                        prerequisites.Count == 0
                            ? "."
                            : ", together with the documents those prompts read — an OpenSpec layout "
                                + "and process documents, outside the prompt directory. Anything this "
                                + "repository already had is untouched and absent from this branch."
                    )
                    + " Installed from the portal (#229, #269); review and merge to make them "
                    + "available to the Automations already created.",
                cancellationToken
            );

            if (published.IsError)
            {
                return new InstalledStarters(
                    [],
                    null,
                    null,
                    published.FirstError.Description,
                    [],
                    []
                );
            }

            // Split back apart for the report: the installer answers in paths, and which list a path
            // belongs to is a fact this handler already holds.
            var promptPaths = new HashSet<string>(files, StringComparer.Ordinal);

            return new InstalledStarters(
                [.. published.Value.Written.Where(promptPaths.Contains)],
                published.Value.PullRequestUrl,
                published.Value.PullRequestUrl is null ? null : branch,
                Failure: null,
                [.. published.Value.Written.Where(path => !promptPaths.Contains(path))],
                published.Value.Skipped
            );
        }
    }
}
