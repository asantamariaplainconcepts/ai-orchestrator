# Ship a change unattended

Issue: [#343](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/343) ·
Change/spec ID: `ship-a-change-unattended`

## Why

One change costs the owner three sittings — approve the grill, read the spec and clear the hold,
read the diff and clear the hold again. For work whose shape was already accepted at grill, the two
later gates buy nothing and cost a context switch each. This repository is also the first customer
of the loop the product ships; the unattended tier is intended, and running it on ourselves is how
it earns the right to be offered.

Traceability is Foundation, through the `/aio:*` workflow framework row in
[`09-foundation-vs-product-split.md`](../../../docs/product/mvp/09-foundation-vs-product-split.md)
rather than a `UC-*` (RULE-003, RULE-005). Main actor **ACT-001 Admin** — the repo owner operating
the repository's own loop, solo (DEC-003); no new actor is invented. No `OPN-*` blocks it: the
governing decision was taken at grill by the product authority and is recorded **in this change**
(RULE-006 is satisfied by closing the question, not by assuming an answer).

## What Changes

- **New command `/aio:ship <issue>`** — carries a `status:ready-for-proposal` issue to a
  squash-merge on `main` in one unattended run: OpenSpec change → draft PR → implementation on the
  same branch → retro → archive → CI-green precondition → linted squash-merge → deploy watch →
  `status:done`. It requests no human input between the invocation and the merge.
- **It runs the three staged commands in unattended mode** rather than restating their steps, so the
  gate orderings keep exactly one owner (ADR-0003). Unattended mode is one explicit clause in each:
  `/aio:propose` and `/aio:implement` advance the status **without** applying the hold; `/aio:sync`
  treats the invocation as DEC-016's recorded go-ahead and derives the retro reflections and the
  squash subject without presenting them for confirmation. Nothing else about the three differs, and
  **no hold is ever applied on the happy path** — the hold exists to pause *between* commands, and an
  unattended run has no between. Nothing clears a hold, so the invariant survives literally rather
  than by exception.
- **A halt applies the hold.** Every refusal the three commands already make becomes, in unattended
  mode, a halt that applies the hold and comments why — CI red or pending, the WIP cap, or a question
  the issue and its spec do not answer. The halted issue then reads like every other issue waiting on
  a person — one `status:*` label plus the hold — and the ordinary staged command for that label
  resumes it once a person clears it.
- **The record says it was unattended.** The PR body and the retro entry both state that the change
  landed with no human reading the spec or the diff, and name `/aio:ship`. This replaces DEC-016's
  in-session go-ahead, which an unattended run has nobody to ask.
- **The decision is recorded** as ADR-0027 plus DEC-068: a change may reach `main` unreviewed on one
  explicit invocation; the two-gate path stays the default; the hold-clearing invariant is preserved.
- **Not BREAKING.** Every existing command keeps its gates, its refusals and its hold behaviour
  unchanged; `/aio:ship` is an additional route, and no integration contract (CI, commitlint,
  Aspire, outbox) changes.

## Capabilities

### New Capabilities

None. `/aio:ship` is a requirement of the existing `workflow-commands` capability; a separate spec
would split one loop's rules across two homes.

### Modified Capabilities

- `workflow-commands`: adds the unattended run as a requirement (its gates, its halt contract, its
  record); amends *the commands are the public API* to name `/aio:ship`; amends *propose opens a
  draft PR and nothing else*, *implement respects the WIP cap and the same PR* and *sync verifies
  green before suppressing any signal* to each carry their unattended clause; amends *clearing the
  hold is the approval* to govern the reviewed path — all without weakening *the hold is a refusal,
  and no command ever clears it*.
- `issue-lifecycle`: amends *two gates and two review stages* — the two gating states still gate,
  but the two review stages are properties of the reviewed path, not of every change.
- `contributor-docs`: the unattended lane joins the solo path and the spec-less lane as documented
  fact in `CONTRIBUTING.md`, not folklore.

## Impact

- **New:** `.claude/commands/aio/ship.md`; `docs/adr/0027-*.md`.
- **Modified:** `.claude/commands/aio/propose.md`, `implement.md` and `sync.md` — one unattended
  clause each, additive, changing nothing about a direct invocation; `AGENTS.md` (command list + the
  hold's section); `CONTRIBUTING.md` (loop diagram, lanes, the recorded-review row);
  `docs/product/mvp/10-locked-mvp-decisions.md` (DEC-068).
- **Unchanged on purpose:** `.claude/workflow.json` (nothing new is tunable — no lane label, no cost
  or size ceiling), `.claude/commands/aio/grill.md`, `refine.md` and `status.md`, every skill
  (`/aio:ship` adds no skill and needs none — *one responsibility per skill, and no skill calls
  another* is untouched), and all product code.
- **Risk this change accepts:** a defective change can reach `main` with no human having read it.
  What bounds it is CI as the only automated reviewer, the WIP cap, the halt-on-ambiguity rule, and
  the fact that every run leaves a PR, a retro entry and one revertible commit.
