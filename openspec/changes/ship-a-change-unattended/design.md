# Design — ship a change unattended

## Context

The loop this repository runs on itself has two human review stages, each marked by the hold: after
`/aio:propose` (the spec, as a draft PR) and after `/aio:implement` (the code, as a ready PR). Two
spec requirements make that structure load-bearing —
[`workflow-commands`](../../specs/workflow-commands/spec.md) *the hold is a refusal, and no command
ever clears it*, and *clearing the hold is the approval*. The first is absolute: nothing in the
repository may remove the label. The second describes the mechanism the first protects.

An unattended route therefore cannot be built by chaining the existing commands as they stand — each
ends by applying a hold that the next one is forbidden to clear. The invariant is not an obstacle to
route around; it is the thing that makes a hold trustworthy, so the design's job is to add a route
that never creates a hold rather than one that learns to remove one.

Constraint that shapes everything else: [`sync.md`](../../../.claude/commands/aio/sync.md) is 128
lines of deliberately ordered gates, several of which exist because a specific failure cost days
(#202's five red deploys; the `[skip ci]`-before-green ordering). Any design that copies that
ordering into a second file is a design that will drift.

## Goals / Non-Goals

**Goals:**

- One invocation carries an unheld `status:ready-for-proposal` issue to a squash-merge on `main`,
  requesting no human input in between.
- Every gate, ordering and guarantee of the staged path applies to the unattended path **by
  construction**, not by restatement.
- Nothing, on any path, ever removes the hold.
- A halt is indistinguishable from ordinary work-awaiting-a-person, and resumes through the ordinary
  commands with no repair step.
- The absence of review is legible afterwards from the PR and the retro log alone.

**Non-Goals:**

- No durable lane marker on the issue (rejected at grill).
- No eligibility restriction — any issue the owner approves may take the route.
- No cost ceiling, size ceiling, batching, or auto-retry.
- No change to the WIP cap's meaning, or to the overlap check's advisory status.
- No change to the product's own story hold (BR-007) or to any product code.

## Decisions

### D1 — Reuse by invocation, with one explicit unattended clause per command

`/aio:ship` runs `/aio:propose`, `/aio:implement` and `/aio:sync` in **unattended mode**. Each of the
three carries one additive clause stating everything that differs; `/aio:ship` itself states no gate,
no ordering and no guarantee of its own.

What each clause says:

| Command | Unattended clause |
|---|---|
| `propose` | advance `status:ready-for-implementation` **without** the hold |
| `implement` | set `status:code-review` **without** the hold; the WIP cap is enforced unchanged |
| `sync` | the invocation is DEC-016's recorded go-ahead; the retro reflections and the squash subject are derived and recorded without being presented for confirmation, and the entry marks its reflections unconfirmed |
| all three | every refusal becomes a halt that applies the hold and comments the reason |

*Rejected — `/aio:ship` orchestrates the skills directly* (`openspec-propose`, `open-pr`,
`openspec-apply-change`, `mark-pr-ready`, `retro-entry`, `openspec-archive-change`, …). It avoids
touching the three command files, but `/aio:ship` would then have to restate sync's gate ordering —
CI-green before the `[skip ci]` commit, lint before merge, deploy watch after — giving that ordering
two owners. ADR-0003 (*a derived artifact has exactly one owner*) is precisely about this, and #202 is
what a drifted ordering costs.

*Rejected — extract a shared `close-out-and-merge` skill* used by both `sync` and `ship`. It would
give the ordering one owner, but a skill that gates CI, writes a retro, archives a bundle, lints a
message, merges and watches a deploy violates *one responsibility per skill*
([`skill-catalog`](../../specs/skill-catalog/spec.md)), and the refactor puts the repository's most
consequential ordering through a rewrite in order to add a route beside it.

*Rejected — a `--no-hold` flag on each staged command.* Identical in effect to the clause, but it
invites a human to pass it, which is a way to skip a review stage without noticing. Unattended mode
is reachable only through `/aio:ship`, so choosing it is always a deliberate, named act.

### D2 — Traverse every lifecycle state, do not jump to `done`

The run sets `ready-for-implementation`, `in-progress`, `code-review` and finally `done`, exactly as
the staged path does. This falls out of D1 for free, and it is what makes a halt resumable: the
issue's single label is a truthful statement of where the work stopped, so the ordinary command for
that label picks it up.

*Rejected — set only `done` at the end.* A halt would then leave an issue reading
`status:ready-for-proposal` while a branch, a PR and possibly commits exist — the exact drift
`/aio:status` is built to report as a defect, and no command would know where to resume.

### D3 — A halt applies the hold

Applying a hold is permitted to any command; only removing one is forbidden. So a halt writes the
hold, comments the specific reason, and leaves the status label alone. The result is an issue that
reads exactly like one parked at a review stage, released by the same single act.

*Rejected — report only, touch nothing* (offered at grill, declined). An abandoned run would be
indistinguishable from work in flight, and nothing on the issue would record that it stopped.

### D4 — The record carries the absence of review

DEC-016 exists because GitHub forbids self-approval: the recorded gate is the human's in-session
go-ahead plus the PR checklist. An unattended run has nobody to ask, so the invocation is the
authorisation — and that substitution is only honest if it is visible. The PR body and the retro
entry each state that no human read the spec or the diff, and name `/aio:ship`.

This also keeps the retro log usable as evidence: without the marker, a future reader cannot tell
which changes were reviewed, and any claim about the route's safety would be unmeasurable
(ADR-0018 — *a measurement licenses only what it measured*).

### D5 — CI is the only automated reviewer, and it is not softened

Nothing about the check rollup, its timing, or the `[skip ci]` ordering changes. On this route CI is
the sole thing standing between a generated diff and `main`, so the one gate that already refuses
gets no new exceptions.

## Risks / Trade-offs

- **A defective change reaches `main` with nobody having read it.** → Bounded by: CI green as a hard
  gate; the WIP cap; halt-on-ambiguity instead of guessing; and the fact that every run leaves a PR,
  a retro entry and exactly one revertible squash commit. Accepted deliberately — this is the route's
  entire premise, not an oversight.
- **The unattended clause is conditional behaviour in three command files.** → Each clause is a few
  lines, additive, and adjacent to the rule it modifies (ADR-0007 — *an edit lands on a site that was
  read*). The alternative concentrates the conditional in one file at the cost of duplicating the
  ordering it depends on.
- **"A question the issue does not answer" is a judgement, not a gate.** A model that under-detects
  ambiguity ships a guess. → The spec requires the halt; the retro entry states the reflections were
  unconfirmed, so a bad judgement is at least legible afterwards. First recurrence graduates to an
  ADR (*lessons graduate at the second occurrence*).
- **This change will eventually ship itself this way.** → Not on its first run: this one goes through
  the staged path, which is what puts a human in front of the ADR that authorises the route.
- **A halt mid-implementation leaves commits on a branch with no review stage having occurred.** →
  The hold plus the comment make the state explicit, and the resumption path is an ordinary
  `/aio:implement`, which applies its own hold at the end as usual.

## Known divergence this change creates, deliberately

`src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/workflow/` ships **byte-identical
copies** of the six `/aio:*` command files as the product's spec-first starter tier — verified against
`origin/main` for all six. This change edits `propose.md`, `implement.md` and `sync.md` in
`.claude/commands/aio/` and **does not mirror them**, so three of the six copies now diverge.

That is the correct outcome for this change, on two grounds. #343 puts product-side changes out of
scope; and shipping an unreviewed-merge route into other people's repositories is a far larger
decision than ADR-0027 made — its evidence is one solo maintainer's own machine, which is precisely
the distinction ADR-0021 drew when it permitted a capability in self-host and refused it in a
deployment. Each tree stays internally consistent: the starter's copies keep the reviewed loop and
reference no `ship.md`.

The risk is that the divergence is **silent**. `StarterCatalogue_Should_Constraint` checks frontmatter,
bodies, path collisions and wiring, and still does not compare the two trees — the gap #323's retro
recorded. So this change records the divergence here and carries it to a **tracked issue** at sync
(ADR-0026): whether the starter tier gets the unattended route is a product decision, owed its own
grill, and "the two trees are no longer identical" needs to be discoverable by whoever next assumes
they are.

## Migration Plan

Additive; nothing to migrate. Every existing issue, branch, PR and command behaves as before, and a
contributor who never types `/aio:ship` sees no difference. Rollback is deleting
`.claude/commands/aio/ship.md` and the three unattended clauses — no state, schema or label depends
on them.

## Open Questions

None. The one open question — whether a change may reach `main` unreviewed — was closed at grill by
the product authority (DEC-003) and is recorded by this change as ADR-0027 + DEC-068.
