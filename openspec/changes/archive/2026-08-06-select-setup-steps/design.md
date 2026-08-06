# Design: select-setup-steps

## Context

The setup card is three moves: look, confirm, report. #229 built the look (`DiscoverPipeline` —
propose, never pick). #233 moved the per-step detail from the report to before the button, as a plan
computed from the listing discovery had already read. What #233 did not add is any way to *change*
the plan, so the confirm remains all-or-nothing.

Current state, verified in this repository:

- `DiscoverPipeline.PlannedStep(Trigger, PromptFile, Exists, Gated, Installable)` —
  [DiscoverPipeline.cs:66](../../../src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Automations/UseCases/DiscoverPipeline.cs). The plan is
  built at lines 119–140 and carries no hand-off information.
- `SetUpDefaultAutomations.Request(PromptDirectory, InstallMissing)` —
  [SetUpDefaultAutomations.cs:72](../../../src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Automations/UseCases/SetUpDefaultAutomations.cs).
  `adopted` and `gaps` are computed at lines 164–167 and walked as one sequence at 169.
- `FillGaps` already short-circuits on an empty gap list (lines 315–318): no branch, no pull
  request, clean success. One layer below, `StarterInstaller.Install` returns
  `WorkspaceErrors.NoChanges()` for an empty file list
  ([StarterInstaller.cs:37](../../../src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Automations/StarterInstaller.cs)),
  which would reach the card as `installed.failure`.
- The card's `Plan` component holds no selection state, and the confirm hardcodes
  `installMissing: true` —
  [WorkflowSetupSection.tsx:85](../../../src/frontend/features/automations/WorkflowSetupSection.tsx).

Constraints that bind the design: BR-003/DEC-056 (trigger identity is case-insensitive, and the same
comparison must be used everywhere), BR-009 (`ManageAutomations`), the convergence promise in
`default-automations` (running the action twice creates nothing), and DEC-021 (no hardcoded JSX
copy — CI fails on it).

## Goals / Non-Goals

**Goals:**

- An Admin can exclude any proposed step, of either kind, before anything is created.
- An excluded step produces no Automation and no repository write.
- A hand-off broken by exclusion is visible at the moment of choosing, not discovered in the report.
- Zero behaviour change for every existing caller of the setup action.

**Non-Goals:**

- Editing a row's wiring in the plan — trigger label, `requiresApproval`, prompt directory. Those
  are `CreateAutomation`'s questions and stay there.
- Remembering a declined step. Nothing is persisted; see D6.
- Deselecting Automations the project already has. Exclusion prevents creation; it never deletes or
  disables. UC-006 owns that.
- Extending the E2E GitHub stub so plan rows become reachable in that tier. Its own change.

## Decisions

### D1 — The client names which steps, never what to do with them

`Request`/`Command` gain `IReadOnlyList<string>? Steps`: the selected triggers. The server keeps
deriving adopt-versus-install from its own fresh directory read; selection is a filter applied to
that derivation, never a plan supplied from outside.

*Why.* The repository can change between the discovery read and the confirm — a file appears, a
file is deleted. If the client sent decisions ("wire this", "install that"), a stale plan would
write a starter over a file that now exists, and a client could name a write the server never
proposed. Filtering keeps the server the only thing that decides what happens to a repository.

*Alternative rejected:* sending the `PlannedStep` rows back as the instruction. It reads as the more
literal "confirm what you saw", but it makes the client's stale snapshot authoritative over the
server's fresh read, and turns a filter into a write primitive.

### D2 — Absent means every step; empty means none

`Steps: null` (or an absent body) is every step — this is what preserves the #212 bodyless call
exactly. `Steps: []` is a lawful no-op: nothing created, no pull request, and a report saying every
step was excluded.

*Why spell it out.* Conflating "no selection sent" with "nothing selected" is the classic
nullable-collection bug, and here the two readings differ by a pull request. Each gets its own
scenario in the spec so neither can be implemented by accident.

The card additionally disables the confirm control when no row is selected — pressing a button that
provably does nothing is a worse answer than not offering it. The API still accepts the empty list
rather than rejecting it, because a lawful no-op is not an error, and defence in depth costs one
branch here.

### D3 — `InstallMissing` stays

*Alternative rejected:* remove it, and let `Steps: null` mean "every step, and install the gaps".
Tempting — the card has hardcoded `installMissing: true` ever since #233 deleted the checkbox, so
the flag looks vestigial.

Rejected because a bodyless `set-up-defaults` call today creates Automations and writes **nothing**
to the repository. Folding install into the new default would silently turn that same call into one
that opens a pull request. Surprising somebody's repository is the exact failure this whole feature
exists to prevent, and it is not worth spending on a field cleanup.

Keeping it makes this change strictly additive. Note the two are orthogonal and compose without
ambiguity: selection decides *which* steps, `InstallMissing` decides whether gaps are *written*. A
selected gap with `InstallMissing: false` is wired and reported as a missing prompt — the existing
behaviour, unchanged.

This does not conflict with #233's requirement that no separate consent be required for installing
the starters the plan names: that requirement constrains the **surface**, and the surface still
offers no such control.

### D4 — Exclusion is filtered before the skip logic, and reported as its own list

The filter is applied where `adopted` and `gaps` are computed
([SetUpDefaultAutomations.cs:164–167](../../../src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Automations/UseCases/SetUpDefaultAutomations.cs)),
upstream of the taken/overlap checks in the loop. An excluded step therefore never reaches the skip
path and can never appear in two lists at once.

Filtering `gaps` there also means `FillGaps` receives only selected gaps, so its existing
`gaps.Count == 0` short-circuit is what satisfies "no pull request when nothing is left to install".
`StarterInstaller.Install` must never see an empty list — it would answer `Workspace.NoChanges` and
the card would report a failure for a thing the Admin chose.

`Response` gains `IReadOnlyList<string> Excluded` rather than a new `SkippedStep.Reason` value.
*Why.* "Skipped" means the project already had it; that count answers "was this already set up?".
Folding the Admin's own choice into it makes the one number that means "already there" mean two
things.

### D5 — The hand-off marker is computed on the client, and needs one new field

`PlannedStep` gains `IReadOnlyList<string> OutputLabels`, mirrored as `outputLabels: string[]`.

*Why client-side.* The marker must update on a click. A server round-trip per checkbox is latency in
the middle of a decision, and the data needed is one array per row that discovery already has in
hand.

*Why not reuse `buildChains`.* [workflowGraph.ts:41](../../../src/frontend/features/automations/workflowGraph.ts) answers a
different question over a different type: it walks created `Automation`s (needing `id` and
`enabled`) to lay out the canvas. Plan rows have neither. Forcing rows into that shape to reuse it
would be the larger coupling. A small pure function over plan rows is the honest answer — with one
rule carried across deliberately: **an edge exists from A to B when A's output labels name B's
trigger.**

One thing must *not* be carried across: `buildChains` compares trigger labels through a plain `Map`,
which is case-sensitive. The product identity is case-insensitive (BR-003, DEC-056). The plan path
compares case-insensitively. (The canvas gets away with it because both sides come from the same
catalogue; the plan path should not inherit a latent bug.)

Confirm is never blocked by a gap. The workflow already tolerates one — *"Where nobody hands on, a
person must"* (`docs/product/manual/README.md`) — so a break is information, not an error.

### D6 — Deselection is per-invocation

No column, no migration, no new state. Every invocation proposes every step, selected.

*Why.* Remembering a decline means the product must also decide when to forget it — a new starter
enters the catalogue, the team changes its mind, someone else runs setup. That is a policy nobody
has asked for. It also preserves convergence: `default-automations` promises that after the action
the wired set exists, and a stored exclusion would make that promise conditional on history.

*Consequence, accepted:* an Admin who excludes a step and re-runs setup sees it proposed again. That
is the correct default — the alternative silently hides steps from a colleague who never made the
choice.

### D7 — Incidental: a comment that contradicts its code

[DiscoverPipeline.cs:109–110](../../../src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Automations/UseCases/DiscoverPipeline.cs)
claims a step that neither exists nor is installable "is not silently dropped — it is listed as not
installable". Line 139 (`.Where(step => step.Exists || step.Installable)`) drops it. The code is
right: such a step can never be created, so it does not belong in a list titled *what this will
create* — and once rows are selectable, an unselectable row that does nothing either way would be
pure noise. The comment is corrected, not the filter.

## Risks / Trade-offs

**A selection sent for a step that no longer exists in the plan** (the repository changed between
discovery and confirm) → the filter is a set intersection, so an unknown trigger simply matches
nothing. No error, no invented work. Covered by a scenario.

**Two ways to end up with no starter pull request** — everything already in the repository, or
everything deselected → both converge on the same `FillGaps` short-circuit, so there is one code
path and one behaviour, not two that can drift.

**The gap marker is frontend logic with no unit-test runner in this repository**
(`src/frontend/package.json` has only `lint`, `typecheck`, `build`) → the data contract it depends on
is asserted in the functional tier (that `outputLabels` reaches the card correctly), and the
function itself is covered by `tsc` plus review. Stated rather than papered over; adding a frontend
test runner is its own change.

**E2E cannot reach the plan rows.** [SetupPlan_Should_Constraint.cs:18–24](../../../src/tests/AiOrchestrator.EndToEndTests/SetupPlan_Should_Constraint.cs)
already records why: rendering the plan needs a Connector serving directory listings, and that
tier's GitHub stub answers issues only → selection behaviour is covered in the functional suite,
where listings can be arranged. No E2E test is written that cannot honestly reach the state.

**`rtk` can mask a failed frontend build** (recorded in this repository's own history: a build
returned 0 while broken, invalidating a mutation check) → the verification tasks run the frontend
build through `rtk proxy` and grep the new copy out of the emitted bundle before any result is
trusted.

## Migration Plan

None required. No schema change, no data backfill, no config. The API change is additive in both
directions: an old client omits `steps` and gets today's behaviour; a new client's `excluded` field
is ignored by nothing, since the only consumer ships in the same artifact. Rollback is a revert.

## Open Questions

None. Every question the Definition of Ready raised was closed on the issue before it was marked
ready, and no `OPN-*` decision is open.
