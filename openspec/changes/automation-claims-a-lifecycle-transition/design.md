## Context

Today the pipeline is a **derived graph**: an edge exists exactly where one Automation's output label
equals another's trigger label, and nothing about the shape is stored (DEC-053,
`openspec/specs/automation-configuration/spec.md:437-459`). One field carries both meanings —
`Domain/Automation.cs:54-56` says `OutputLabels` is "the workflow's outgoing edges, **and** any mark
that goes with them."

That derivation is drawn twice (the Automations canvas and the Backlog board) and walked six times,
and the walks disagree; ADR-0022 records the evidence and the decision this change implements. What
matters for the design is the consequence: **an arrangement has no stored home, so neither of #310's
two complaints can be expressed.** There is no "before the first step" because the first step's
trigger *is* the entry point, and there is no order to change because the order is recomputed from
labels at every read site.

The mechanism that makes the fix cheap already exists. A Run applies its output labels through
UC-008's licensed write (`src/modules/Runs/.../Features/Execution/RunExecutor.cs:196-231`); they land
at the vendor, come back as an ordinary `StoryChanged`, and are matched like any other label
(`Features/Matching/StoryChangedHandler.cs:59`, case-insensitively). **A person moving a label is
already the same mechanism as an Automation applying one** — so a transition nobody claims needs no
representation at all. Nothing fires until a person moves the label, and that is the whole feature.

**Design-system governance.** All UI here is governed by `docs/design-system/` and the derived
`DESIGN.md`, with the generated tokens as the only source of visual value (DEC-051): no colour,
spacing or size literal outside them. Copy resolves through the typed English catalogue
`src/frontend/shared/i18n/en.ts` (DEC-009, DEC-021) — hardcoded JSX copy fails CI.

**Backend conventions.** No deviation. Everything stays inside BC-001 Project Configuration:
`Features/Automations/UseCases/<UseCase>` vertical slices, the custom CQS mediator, `ErrorOr` +
`ProjectErrors` + `ApiResults.Problem`, FluentValidation, and the module's own `projects` schema. The
Runs module continues to see Automations only through
`AiOrchestrator.Modules.Projects.Contracts/IAutomationCatalog.cs`.

## Goals / Non-Goals

**Goals.** Store the lifecycle as an ordered list of stages. Make an Automation claim exactly one
transition along it. Make an unclaimed transition read as a person's turn. Make the board the one
place the arrangement is authored, with the canvas deleted. Separate the transition from a mark.
Carry every configured hand-off across without loss.

**Non-Goals.** Genuine branching (removed here, not re-added). An edge id or a graph table. Folding
human review into `requiresApproval`. Turning `UpdateAutomation` into `PATCH`. A stage-list editor —
renaming a stage, removing an unused one, seeding a lifecycle for a brand-new project. Splitting
`AutomationsSection.tsx` (888 lines). Any new Run state on the board.

## Decisions

### D1 — The lifecycle is an ordered array of stage names on the Project

`Project` gains `LifecycleStages`, a `character varying(200)[]` column whose **array position is the
order**. It is owned by BC-001 and served over the API; nothing recomputes it.

An array rather than an owned entity (`ProjectLifecycleStage(Order, Name)`, the shape
`ProjectRuntimeCredential` uses): order is intrinsic to an array, so reordering is one write of one
value rather than a renumbering that can leave gaps or duplicates mid-transaction. The precedent is
in the same schema — `OutputLabels` is already `character varying(200)[]`
(`Persistence/ProjectsDbContext.cs:53-62`), and Npgsql maps a `List<string>` natively, so there is no
delimiter to parse.

*Alternative rejected — an owned table.* It buys per-stage identity, which is exactly what a rename
or a prune would need, and both are out of scope. Adding identity for a capability we are not building
would be the second description ADR-0022 forbids.

### D2 — An Automation stores its claimed transition; the from-stage is its trigger

The from-stage is the Automation's existing `TriggerLabel` — that is already how it fires, and giving
it a second name would be a second description of one fact. The to-stage is a **new single-valued**
`ToStage`: the label the Run applies as the lifecycle move.

*Alternative rejected — derive the to-stage from adjacency in the stored list.* It stores strictly
less, and it is wrong: reordering the list would then silently rewrite what every neighbouring
Automation hands on, which is ADR-0019's invisible-at-the-call-site failure in a new field. AC 5
requires that moving one Automation change **no other** Automation's claimed transition; under
derivation, an insertion would change several. Recorded in ADR-0022's Alternatives too, because it is
the tempting one.

### D3 — `ToStage` is nullable: at most one transition, never two

AC 13 reads "it names exactly one claimed transition". Taken literally that would forbid an Automation
that claims none — and DEC-053's standalone Automation survives this change: `ai:estimate` is "a
trigger that acts on its own when somebody applies its label", and the last stage of a lifecycle has
no outgoing boundary at all (AC 8). Both must stay expressible, and today's chain end is exactly this
(`workflowGraph.ts` `next === null`).

So the design reads AC 13 as **at most one, and never two** — its force is that branching is
unrepresentable. `ToStage` null means "claims no transition: it acts, it may mark the Story, and the
flow ends there." This is the one place the design does not take an acceptance criterion at its word,
so it is stated here rather than absorbed; see Open Questions.

### D4 — A claimed transition names two adjacent stages, and the write is what keeps it true

Storing both the order and each claim makes disagreement possible: an Automation could claim
`s1 → s3` while the list says `s1, s2, s3`. The invariant is that a claim names **two adjacent stages
of the project's lifecycle**, and it is maintained by the operation that claims, not by a nightly
repair:

- Claiming `sX → sY` where `sY` is already the stage after `sX`: the claim is stored, the list is
  untouched.
- Claiming `s0 → s1` where `s0` is not yet a stage: `s0` is inserted immediately before `s1`, and the
  order of every existing stage is unchanged (AC 4).
- Moving a claim to an existing boundary: the claim changes; the list does not (AC 5).

A domain guard in `Domain/Project.cs` refuses a claim naming non-adjacent stages, and it is the only
place the rule lives — a second implementation of an invariant is how the two come to disagree
(`OverlapGuard.cs:9-13` records that lesson for BR-003).

### D5 — BR-003 needs no new enforcement; it acquires its true meaning

"One claimant per transition" under a linear lifecycle is "one enabled Automation per from-stage", and
that is what the product already enforces in three places: the expression index
`IX_automations_trigger_identity` on `(ProjectId, lower("TriggerLabel"), COALESCE(lower("TriggerState"), ''))`
(`20260729150023_UniqueAutomationTrigger.cs:25-30`), `OverlapGuard.cs:35-53` in memory where it can
name the conflicting Automation, and the client-side explanation. All three stay. The refusal already
names the Automation already holding the trigger (`OverlapGuard.cs:78-81`), which is what AC 6 asks
for, and it is already case-insensitive on both sides (DEC-056), which is what AC 6's second sentence
asks for.

What changes is only the client-side explanation's home: `chainDrag.ts:58-67` and
`AutomationsSection.tsx:129-132` today; the boundary control after this change.

### D6 — The six walks collapse into one read, and three of them collapse to nothing

Named individually, because "they get simpler" is a claim that has to survive contact with each site:

- `workflowGraph.ts:41` (`buildChains`) — stops deriving a graph. What remains is the stage list read
  from the API, plus a lookup from stage to the Automation claiming the transition out of it.
  `WorkflowChain`, `branches`, `branchedFrom` and `hasBranches` go (AC 13).
- `chainDrag.ts:80` (`reaches`) — **goes entirely.** A cycle is unrepresentable in a linear ordered
  list, so the loop refusal has nothing to compute. Of the four refusals, `self` and `cycle` become
  impossible by construction and `already` becomes "this boundary is where it already is"; only
  `shared` (BR-003) survives, and that is D5's.
- `planHandoff.ts:35` — keeps working on **uncreated plan rows with no ids**
  (`planHandoff.ts:6-13`); any design needing an id here is wrong. Its question ("which selected steps
  lost their provider?") becomes "which selected steps' from-stage nobody claims a transition into",
  answered over the plan's own claims. Its deliberate case folding (`planHandoff.ts:16-20`) becomes the
  norm rather than the exception.
- `KanbanBoard.tsx:98-137` — stops walking labels. Columns are the stage list, in the stored order.
  The invented ordering rule (`:131-136`, unchained Automations after the flow) disappears: an
  Automation that claims no transition contributes no stage, and a Story carrying its trigger label
  needs no column it did not already have.
- `BoardPreview.tsx:33-35` — its dedupe existed because branch rows re-entered at an existing column;
  with no branches there is nothing to dedupe. It survives, re-parented (D7).
- `AutomationNode.tsx:31` (the dangling badge) — **goes.** It warned that an output label points at no
  Automation. After the split that is not a fault but the normal case: a label that names no stage is a
  mark. Removing a warning is a judgement, so it is called out here rather than buried in a task.

### D7 — The board is the authoring surface, and the preview is re-parented

AC 11 names exactly three deletions — `WorkflowCanvas.tsx`, `Connector.tsx`, `DropSlot.tsx` — and
lists `BoardPreview.tsx` and `AutomationNode.tsx` among the six sites to *update*. `WorkflowCanvas` is
`BoardPreview`'s only caller today (`WorkflowCanvas.tsx:264`), so the preview moves up into
`AutomationsSection.tsx`, where it becomes the Automations tab's read-only picture of the lifecycle
the catalogue produces. That reconciliation is an inference from AC 11 plus the six-site list, and it
is declared rather than assumed silently.

Every arrangement change is offered by an explicit control **and** by dragging, and both call the same
function (AC 12). This is not a preference: **Playwright cannot perform an HTML5 drag** — recorded at
`WorkflowCanvas.tsx:248-252`, citing #110 — so routing the button through the same function "is what
puts this logic under test at all." Consequently **no acceptance criterion is written as "when I drag
X, then Y"**; the criteria assert rendered state, and assert outcomes through the explicit control (ACs
5, 6, 9, 12). The gesture itself stays uncovered, and this change does not pretend otherwise.

The board already mutates Automations from a click today — the "require a person" button on a column
header (`KanbanBoard.tsx:337-361`) — so this extends an existing authoring path rather than opening a
new one.

### D8 — One request builder, and a field the board is already clearing

`requestFor` (`automations/automationRequest.ts`) is ADR-0019's one builder. `KanbanBoard.tsx:348-360`
does **not** use it: it restates eight fields inline and omits `model`, which `requestFor` carries. Read
from the code, not yet exercised: pressing "require a person" on a board column therefore reverts that
Automation's chosen model to the deployment's — the same failure #291 produced and ADR-0019 was written
for. The new claimed-transition field would be the second one lost the same way, so the board's call
site moves onto `requestFor` as part of this change, and the task verifies the regression before
claiming it fixed (ADR-0001, ADR-0005).

`previewPort` remains absent from the frontend's `Automation`/`CreateAutomationRequest` types
(`automations/types.ts`) — ADR-0019's own recorded, still-open instance. This change does not close it
and does not widen it.

### D9 — The lifecycle move and the marks travel the same write

`RunExecutor.HandOn` (`RunExecutor.cs:196-231`) applies the claimed to-stage **and** every mark through
UC-008's write, keeping #165's guarantees intact: every label is attempted, and the Run fails naming
each one that did not land. Only the board's reading changes — the to-stage is a column, a mark is not
(AC 7). `AutomationDetail` (`IAutomationCatalog.cs:31-56`) gains the to-stage; `StoryChangedHandler`
is untouched, which is the evidence that this model needs no new dispatch machinery.

### D10 — The starter tiers claim transitions, and that is how a new project gets stages

`StarterCatalogue.cs`, `DiscoverPipeline.cs` and `SetUpDefaultAutomations.cs` wire the spec-first tier
(`grill → propose → implement → sync`) through output labels. They now name claimed transitions, so
installing a tier creates stages as a consequence of claiming (AC 4's mechanism) — which is why "seed
a default lifecycle" can stay out of scope without leaving a new project stageless. #310 does not
enumerate these files; the scope is stated here so a reviewer judges it rather than discovers it.

## Risks / Trade-offs

- **A stored order can disagree with what would fire** — the failure DEC-053 avoided. → Tied at the
  write (D2, D4): the from-stage *is* the trigger and the to-stage *is* the applied label, so the
  drawing is what fires; the adjacency guard lives in one place.
- **The migration is the whole risk of this change.** A scaffolded `DropColumn` + `AddColumn` here
  would discard every configured hand-off with the schema perfectly correct afterwards
  (`20260730222648_OutputLabelSet.cs:9-19`). → Hand-written, plus the before/after count assertion in
  the Migration Plan. Not "the migration ran"; the count.
- **Case folding, carried instead of fixed, would drop edges the canvas draws today.**
  `buildChains` compares through a plain `Map`; product identity is case-insensitive
  (`planHandoff.ts:16-20` says so). → Every comparison in the migration and the new code folds case,
  matching `lower()` in the index and `OrdinalIgnoreCase` in matching.
- **The drag gesture has no automated coverage and this change does not add any.** → Stated, not
  worked around (D7). Every arrangement change is reachable and asserted through its explicit control.
- **A lifecycle accumulates stages.** Stages are never pruned. → Accepted; pruning needs a decision
  about what removing a stage means for Stories carrying its label, and that is a separate change.
- **522 lines are deleted, including the only surface some behaviours have.** → The catalogue's CRUD
  is asserted separately (AC 11) so deletion cannot quietly take a capability with it (ADR-0006).

## Migration Plan

One **hand-written** migration in
`src/modules/Projects/AiOrchestrator.Modules.Projects/Persistence/Migrations/`, following
`20260730222648_OutputLabelSet.cs`'s add-copy-drop shape rather than anything scaffolded.

**Up.**

1. `AddColumn "LifecycleStages" character varying(200)[] NOT NULL DEFAULT '{}'` on `projects.projects`.
2. `AddColumn "ToStage" character varying(200) NULL` on `projects.automations`.
3. Derive each claim, case-insensitively: `ToStage` = the **first** of the Automation's `OutputLabels`
   whose `lower()` equals another **enabled** sibling Automation's `lower("TriggerLabel")` in the same
   project. Remove exactly that label from `OutputLabels`; every remaining label — including one that
   matches no sibling trigger — stays as a mark.
4. Build each project's `LifecycleStages` from the chain the board draws today
   (`KanbanBoard.tsx:110-137`): roots first (an enabled Automation nothing hands to), then what each
   hands to, then the loose ones — a `WITH RECURSIVE` walk over the derived claims, so the stored order
   is the order that was on screen.
5. Assert, in the same transaction: the number of `(automation, matched output label)` pairs before
   equals the number of non-null `ToStage` values after. A mismatch raises rather than commits.

**Down.** Lossy by nature and not disguised: prepend `ToStage` back onto `OutputLabels` where it is
non-null, then drop both columns. A project's stage order cannot survive the reverse, because the old
shape has nowhere to put it — written down rather than pretended away, exactly as
`20260730222648_OutputLabelSet.cs:53-80` does.

**Deployment.** `AiOrchestrator.MigrationService` applies it on startup as with every other migration;
there is no feature flag and no read-both-shapes compatibility layer. A compatibility layer over the
one field whose ambiguity *is* the bug would be a seventh re-implementation of the label walk.

**Verification** (ADR-0001 — exercised, never read): a functional test seeds a project with the shape
the board draws today, including a differently-cased edge and an output label matching no trigger, runs
the migration against the Testcontainers Postgres, and asserts the counts and the stored order. The
schema being correct afterwards is not the evidence.

## Open Questions

- **AC 13's "exactly one".** D3 reads it as *at most one, never two*, so that DEC-053's standalone
  Automation and the last stage of a lifecycle stay expressible. If the owner means literally exactly
  one, `ToStage` becomes non-null and both of those cases need an answer before implementation starts.
- **Vocabulary.** "Stage" and "transition" are new nouns beside DEC-005's locked ones. Whether they get
  a locked entry is the owner's call; this change uses them consistently and does not invent a DEC.
- **DEC-053's supersession note.** ADR-0022 supersedes one clause of a *locked* decision. Amending
  `docs/product/mvp/10-locked-mvp-decisions.md:209-221` is the owner's act, so the task that does it is
  marked as needing assent rather than performed unilaterally.
- **The duplicate DEC-053.** A second, unrelated DEC-053 (Connector permissions) sits at `:367` of the
  same file. Reported here; fixing it is not this change's scope.
- **Whether the read-only preview still earns its place.** `BoardPreview` existed because wiring the
  workflow and seeing its effect on the board were two tabs. Once the board *is* the authoring surface
  that reason is gone, and a reviewer could reasonably delete it. D7 keeps it — re-parented and now
  derived from the stored stage list — because AC 11 names exactly three deletions and the six-site
  list asks for `BoardPreview.tsx:33-35` to be *updated*. If the owner would rather it went, that is a
  fourth deletion and one fewer surface to keep in step.
- **Where the boundary control lives at phone width.** The board pages one column per screen
  (`KanbanBoard.tsx:252-273`), so a boundary between two columns has no shared screen to sit on. AC 12
  requires an explicit control at every width the board supports; which affordance carries it on the
  pager is a design-system question for implementation.
