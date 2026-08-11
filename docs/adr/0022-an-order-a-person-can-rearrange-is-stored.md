# ADR-0022: an order a person can rearrange is stored, never derived

- **Status:** Accepted
- **Date:** 2026-08-11
- **Deciders:** repository owner (DEC-003); analysis by the agent proposing #310
- **Tags:** frontend, backend, data model, automations

## Context

DEC-053 — *"the catalogue and the workflow are two things"*
(`docs/product/mvp/10-locked-mvp-decisions.md:209-221`) — settled that a project's Automations are an
inventory, that the ones handing work to one another form a workflow, and that **"membership is
derived from the edges and never stored, so the picture cannot claim a chain that would not fire."**
The reason was sound and still is: a stored flag saying "in the workflow" could disagree with the
labels, and then the drawing would promise a hand-off nobody would perform.

Two things have happened since.

**One sentence has been written six times, and they disagree.** "A hands to B when one of A's output
labels is B's trigger" now lives in:

- `src/frontend/features/automations/workflowGraph.ts:41` — `buildChains`, the canvas's graph.
- `src/frontend/features/automations/chainDrag.ts:80` — `reaches`, a second walk, for the loop refusal.
- `src/frontend/features/automations/planHandoff.ts:35` — a third, over *uncreated* plan rows that have
  no ids, which folds case deliberately **because the other two do not**
  (`planHandoff.ts:16-20` says so in as many words).
- `src/frontend/features/backlog/KanbanBoard.tsx:98-137` — a fourth, which keeps only "the first
  hand-off that lands on a column" and then has to invent an ordering rule the derivation cannot
  supply: unchained Automations come *after* the flow (`KanbanBoard.tsx:131-136`).
- `src/frontend/features/automations/BoardPreview.tsx:33-35` — a fifth, deduplicating columns because
  branch rows re-enter the board at a column that already exists.
- `src/frontend/features/automations/AutomationNode.tsx:31` — a sixth, reduced to a badge saying an
  output label points at nothing.

The case disagreement is not cosmetic: product identity is case-insensitive, enforced in the index
(`20260729150023_UniqueAutomationTrigger.cs:28`) and in matching
(`Features/Matching/StoryChangedHandler.cs:59`) with `lower()` and `OrdinalIgnoreCase`. Two of the six
walks compare with a plain `Map`.

**And the derivation cannot hold the answers people ask it for.** #122 wanted the unchained
Automations to have "their place after the ordered ones" — a position *inside* a thing they are not in
— and was closed as superseded rather than solved; DEC-053's own rationale records that. #310 arrives
with the same shape twice: an Admin cannot place an Automation **first**, because in a derived graph
the first step's trigger *is* the entry point and there is no "before" to drop into; and cannot
**reorder** the flow, because a derived graph has no stored order to change. Neither is a defect in
the derivation. Both are questions the data cannot answer, so every surface answers them locally and
differently.

The distinction the six walks keep blurring is between a **fact** — this label equals that trigger,
which the vendor will act on — and an **arrangement**, which is a person's intent about sequence. A
fact is safe to derive. An arrangement has no other home.

## Decision

Where the product offers a person control over the **order or extent of a sequence**, that sequence
SHALL be stored — as an ordered list owned by exactly one aggregate — and every reader SHALL be
served it by that owner rather than recomputing it.

A derivation SHALL remain the home of anything a person cannot rearrange. Deriving a fact is not the
thing being retired here; deriving an *arrangement* is.

A stored order SHALL NOT become a second description of a fact something else already carries. Where
a mechanism already exists to make the sequence happen — here, a hand-off travelling through the
vendor label (`Features/Execution/RunExecutor.cs:196-231` → `Features/Matching/StoryChangedHandler.cs:59`)
— the stored thing SHALL be the order alone, never an edge identifier or a parallel graph table.

This SHALL supersede one clause of DEC-053: *"Membership is derived from the edges and never stored."*
Everything else DEC-053 locked — that the catalogue and the workflow are two things, and that an
Automation's absence from the workflow is not an omission — SHALL stand unchanged, and the
supersession SHALL be recorded where DEC-053 lives rather than only here.

## Consequences

- **Positive:** the questions #122 and #310 could not express become ordinary writes. "Put this
  first" and "move this later" are edits to a stored list; there is no special case to invent,
  because there is nothing to work around.
- **Positive — the check is the shape, and a test asserts it.** The owner serves the order over the
  API, so a client has nothing left to re-derive: the six walks collapse into one read. What guards
  it is not a convention but three assertions on the artifact (ADR-0004): a functional test in
  `src/tests/modules/Projects/AiOrchestrator.Modules.Projects.FunctionalTests` asserting that the
  order the API serves survives claiming a transition and moving one; the end-to-end suite asserting
  that the board's columns are that order; and, for the migration, an assertion that the count of
  configured hand-offs is identical before and after — never a reading of the schema (ADR-0001).
- **Negative:** a stored order *can* disagree with what would fire, which is precisely what DEC-053
  avoided. The cost is accepted only because the two are tied at the write: the transition an
  Automation claims has its trigger label as the from-stage and the label its Run applies as the
  to-stage, so what is drawn is what fires. An implementation that stores the order without that tie
  reintroduces the failure DEC-053 named, and this ADR does not license it.
- **Negative:** a stored list accumulates. Stages are created by being claimed and are never pruned
  (#310 keeps a stage-list editor out of scope), so a project that has been rewired several times
  carries stages nothing uses until someone decides what removing one means.
- **Neutral:** "stage" and "transition" are new nouns standing beside DEC-005's locked vocabulary.
  Whether they get a locked entry of their own is the owner's call, not this ADR's.
- **Neutral:** this ADR was written `Proposed` in the proposal that noticed the recurrence
  (`docs/adr/README.md:13-16`) and reviewed with it. It was **accepted at the spec review of #310**,
  and the status flip was the last edit it may receive: from here it is immutable, and a change of
  mind is a new ADR superseding this one (`docs/adr/README.md:5-7`).

## Alternatives considered

- **Keep deriving, and make "first" a special case.** Rejected on precedent: #122 was exactly that
  special case — a position inside the workflow for things not in it — and was closed as superseded
  rather than solved.
- **Store an explicit graph: edge ids, or a workflow table.** Rejected. The hand-off already travels
  through the vendor label, so an edge row would be a second description of one fact, and the two
  would drift. #310's out-of-scope list names this and refuses it for the same reason.
- **Store the order, and derive each Automation's to-stage from adjacency in it.** Tempting, because
  it stores strictly less. Rejected: reordering the list would then silently rewrite what every
  neighbouring Automation hands on — ADR-0019's failure exactly, invisible at the call site, in a new
  field.
- **Fix only the case-folding disagreement.** Rejected: it treats a sixfold re-derivation as six
  bugs, and leaves the next surface to write a seventh walk.
- **A shared derivation module, still derived.** Rejected: it would make the six walks agree without
  giving either question — place first, reorder — anything to be true about.

## References

- Issue: [#310](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/310) — an Admin
  arranges a project's whole flow on the board, first step included.
- Supersedes one clause of **DEC-053** (`docs/product/mvp/10-locked-mvp-decisions.md:209-221`).
  A *different* DEC-053 (Connector permissions) exists at `:367` in the same file — a docs defect
  worth its own report, and not the decision cited here.
- Related: **DEC-056** (a trigger's identity is the vendor's), ADR-0003 (a derived artifact has
  exactly one owner), ADR-0019 (a whole-object replace has one builder), ADR-0006 (a capability is
  not added until a user can reach it), ADR-0001 (verify claims by exercising them).
- Normative text amended by the change that carries this ADR:
  `openspec/specs/automation-configuration/spec.md:437-459`, `:536-546`, `:559-560`.
