# Design: spec-first-is-the-catalogue

## Context

The setup card reached its current shape over four changes: #212 created the wired set in one action,
#229 asked what the repository already had before creating anything, #233 showed the plan before the
button, #262 made every row of that plan a choice. Each kept one invariant — **the product writes only
prompts, only into the prompt directory, and only where the repository has no file.**

This change breaks the first two halves of that invariant deliberately and keeps the third. It writes
files outside the prompt directory (an OpenSpec layout, process documents), and it writes them for a
tier that today is recognised but never installed. What it does not touch is the rule that an existing
file wins.

Three facts about the current code shape the design, verified rather than assumed:

- `StarterInstaller.Install` already writes **arbitrary repository-relative paths** and creates their
  directories (`StarterInstaller.cs:73-81`). Scaffolding needs no new write seam.
- `IDocumentReader.Read(projectId, path)` already reads an arbitrary repository path from the default
  branch (`IDocumentReader.cs:13`). Presence-checking needs no new read seam either.
- Installability is `Requires is null` and nothing else (`PipelineSteps.cs:35-36`) — catalogue content
  expressed as one predicate, which is why #229's retro could call it "not a branch in the handler".
  That predicate is what this change replaces.

## Goals / Non-Goals

**Goals:**

- One press installs the spec-first workflow's prompts *and* the documents those prompts read, in one
  branch and one draft pull request.
- The consent is explicit, states its consequence before it is given, and is never persisted.
- An existing file always wins — for prompts, as today, and now for prerequisites too.
- The prerequisite set stays catalogue content. A fork edits the manifest and this behaviour follows.
- The `ai:implement` step is wired to a real file, closing the loop at the step that writes code.

**Non-Goals:**

- Verifying prerequisites before consent. The pull request brings them; there is nothing to check.
- Installing tooling. Files and layout only — no `package.json` edit, no CLI.
- Migrating projects that installed the portable starters. Their Automations keep working untouched.
- Persisting adoption, or any new database column.
- Copying this repository's product corpus (`ACT-*`, `UC-*`, `BR-*`, `DEC-*`) into anybody's repo.

## Decisions

### D1 — installability becomes a function of consent, not of `Requires`

`PipelineSteps.Installable` stops meaning "tiers that require nothing" and becomes
`Installable(consent)` — tiers with no prerequisite, plus tiers the caller named. With the portable
tier gone the first set is empty, so `Installable([])` is empty and an unconsented press installs
nothing.

*Alternative rejected:* keep `Requires is null` and clear `requires` on the workflow tier. That would
delete the honest statement of what the tier assumes — the very sentence the switch must display — to
win a predicate. The prerequisite text is the consent's content, not decoration.

### D2 — `steps` absent means all; `tiers` absent means none. The asymmetry is the point

Two selection fields on one request with opposite defaults, which will look like an inconsistency to
the next reader, so it is written down here and in the spec.

`steps` narrows a plan the card already displayed; absent means "everything you proposed", which is
what preserves #262's promise that a bodyless call behaves as it always did. `tiers` **authorises
writing files outside the prompt directory**. A default that authorises is the wrong default no matter
how convenient; a default that includes everything already shown to a human is the right one.

*Alternative rejected:* one combined field. It would make "I want fewer steps" and "I permit a
repository write" the same gesture, which is exactly the conflation the tiering exists to prevent.

### D3 — prerequisites are manifest content, addressed by path

The manifest's tier gains `prerequisites`: a list of `{ file, path }`, embedded like prompts and read
by the same `Text()` resource loader. `path` is repository-relative and absolute in intent
(`docs/process/definition-of-ready.md`), not a `saveAs` resolved against the Connector's prompt
directory — these files have fixed homes and a prompt directory is irrelevant to them.

Empty directories cannot be committed, so `openspec/specs/` and `openspec/changes/archive/` ship as
`.gitkeep` entries — content, listed in the manifest like everything else, rather than a special case
in the handler.

*Alternative rejected:* a `prerequisites` block per *prompt*. Six prompts share one set of documents;
per-prompt ownership would either duplicate them or invent a merge rule.

### D4 — presence is decided in the clone, and skipping everything is not a failure

`StarterInstaller.File` gains `OnlyIfAbsent`. Prompts pass `false` — their absence is already
established by the gap computation the card showed. Prerequisites pass `true`, and the installer skips
any whose path already exists **in the prepared clone**, which is the authoritative default-branch
content it is about to branch from.

`Install` returns `ErrorOr<InstallOutcome>` — written paths, skipped paths, and a nullable pull-request
URL — instead of `ErrorOr<string>`. Where every file is skipped, nothing is pushed, no pull request is
opened, and the outcome carries no failure. This is #262's lesson applied to a new axis: *reporting a
failure for a state the caller chose tells an Admin their own decision went wrong.* The existing
`files.Count == 0` → `WorkspaceErrors.NoChanges()` refusal stays for a genuinely empty request, which
is a caller bug rather than an outcome.

*Alternative rejected:* `IDocumentReader.Read` per prerequisite path before cloning. It costs a vendor
read per file to answer a question the clone answers for free, and leaves a window between the reads
and the clone in which the answer can change. The clone has no such window.

### D5 — `aio-implement.md` takes the `ai:implement` wiring

With the portable tier removed, nothing contends for the trigger, so the manifest's deliberate
omission is deleted along with its reason. The catalogue's duplicate-trigger refusal keeps its
no-exception form — no gated-claim carve-out, which an earlier draft of this change would have needed.

### D6 — discovery returns the tiers, so the switch states its consequence without a round-trip

`DiscoverPipeline`'s response gains `tiers`: id, title, `requires`, and the prerequisite paths. Same
reasoning #262 used for carrying `outputLabels` — the card must answer "what will this write?" on a
click, so the answer cannot be a request. The data is on the catalogue the plan already walks.

The switch's copy commits to the precise claim rather than an optimistic one: it writes these paths
**where they are not already present**. That is true without any read, and the report afterwards names
what was actually written and what was skipped (AC-8, AC-10).

### D7 — no compatibility shim for the removed steps

`PipelineSteps.Match` loses four names and gains nothing in their place. A repository holding
`triage.md` has it reported as *found, not wired* — visible, in the surface that already exists for
exactly this ("a file that matches no step is somebody's document"). Keeping recognition for prompts
the catalogue no longer ships would mean the manifest is no longer the whole mapping, which is the
property #229's retro identified as the one worth having.

### D8 — what the seed contains, and what it cannot

- **Real content:** `docs/process/definition-of-ready.md` and the `RULE-001..007` shaping rules it
  cites. The `definition-of-ready` spec forbids the rubric from restating the rules, so shipping the
  rubric alone would land a document of dangling references — the two are one artifact.
- **Structure with an explicit hole:** `openspec/config.yaml` ships its schema and section headings
  with the project-context block marked TODO. Context is the one part that cannot be inherited, and a
  plausible-looking wrong context is worse than a blank one.
- **Skeletons:** the remaining documents the grill reads — actors, glossary, use cases, business
  rules, open decisions — ship as headings plus their ID convention, so the rubric's links resolve.
- **Never:** this repository's product content.

**UI governance:** the switch is composed from the existing kit per `DESIGN.md` and the design-system
artifacts under `docs/design-system/`; copy resolves through the typed `en.ts` catalogue, since
hardcoded JSX copy fails CI (DEC-009, DEC-021). No new token, no new primitive.

### D10 — the catalogue loses every hand-off edge, and #262's marker goes quiet

`mock.ts:139-141` records that `ai:implement → ai:tests → ai:review` are the catalogue's **only**
hand-off edges. Every spec-first prompt carries empty `outputLabels` (`manifest.json`). Removing the
portable tier therefore leaves the catalogue with **no hand-offs at all**, which makes #262's
broken-hand-off marker — shipped the same day as this change is written — unreachable in practice.

The marker's requirement (`automation-configuration`: *"a hand-off broken by exclusion is shown, and
never blocks"*) stays correct and stays in the spec. It simply has nothing to fire on. This is recorded
rather than modified, because the rule is right and the catalogue is what changed.

*Alternative rejected:* give the spec-first tier `outputLabels` so the loop chains (grill → propose →
implement → sync). The real loop does chain, and this is probably the right follow-up — but the chain
this product runs on gates twice on a human (spec review, then code review), so which edges are
automatic is a methodology decision #269 did not ask for and this change should not smuggle in.
`planHandoff.ts` and the marker survive untouched, ready for it.

### D9 — the decision record

`docs/adr/0012-*` plus **DEC-064** (next free; DEC-063 is the highest recorded) in
`10-locked-mvp-decisions.md`, revising DEC-048's rubric clause on the narrow ground the proposal
states: *"the weaker of the two" presumes two.* DEC-048's read-time invariant is untouched — the
`GrillToReady` action still reads the project's own document live, never a bundled one.

## Risks / Trade-offs

- **Every repository that presses the button holds the same readiness bar on day one.** → It is theirs
  to edit from the moment it lands, an existing document is never displaced, and the alternative
  shipped a workflow whose first Run fails. Recorded as a cost in the ADR rather than argued away.
- **An Admin used to the old button now gets nothing installed without consenting.** → The switch sits
  in the plan they are already reading, and the report says plainly that nothing was installed and
  that no tier was consented to. A silent no-op would be the failure here.
- **Adoption regresses for a repository holding `triage.md`, `tests.md`, `review.md`, `explain.md`.**
  → Reported as found-not-wired rather than silently dropped; existing Automations are untouched;
  named in the manual, the proposal, and the retro so it is discoverable after the fact.
- **The blast radius grows: the product now writes outside the prompt directory.** → Draft pull
  request only, never the default branch (unchanged); an existing file always wins; every path is
  catalogue content, enumerable, and asserted by the manifest test.
- **`Install`'s signature changes and both callers move.** → One seam, two call sites, compile-time
  breakage. The alternative — a second install path — is the duplication #229 extracted this seam to
  avoid.
- **#262's broken-hand-off marker becomes unreachable (D10).** → Named in the retro and in
  `mock.ts`'s comment, which currently asserts the opposite and would otherwise become a lie in the
  codebase. The follow-up — chaining the spec-first loop's own labels — is scoped as its own change.
- **The shipped skeletons can drift from this repository's own documents.** → The manifest-enumeration
  test asserts every prerequisite loads with a non-empty body, the same guarantee starters already
  have. Drift in *wording* is acceptable; a prerequisite that fails to load is not.

## Migration Plan

No data migration: nothing about consent is persisted, and no column changes. Deployment is ordinary.

Rollback is `git revert` with no cleanup — because consent is per-invocation, there is no stored state
to unwind. Automations created by a consented press survive a revert and keep working: they name files
in the project's own repository, not in the catalogue. Pull requests already opened are ordinary draft
PRs a human merges or closes.

Existing projects need no action. Their portable-step Automations continue to run, because a Run
resolves the prompt from the project's repository — the catalogue is not in that path.

## Open Questions

- Does `docs/product/manual/README.md` need its setup-card screenshots reshot, or is the prose change
  enough? The prose is in scope either way; the images are a judgement call at implementation.
- Should the spec-first tier declare its own hand-off labels, restoring a chain to the catalogue
  (D10)? Deliberately out of scope here; it wants its own issue, because which of the loop's edges are
  automatic and which wait for a person is a methodology decision, not a wiring detail.
- `DEC-064` is claimed on the current highest recorded number. If another change lands a `DEC-*` first,
  the number moves — the ADR filename and the entry must be renumbered together at sync.
