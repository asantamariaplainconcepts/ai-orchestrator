## Why

At each human stage our own loop asks a reviewer for two acts: read the work, then find and apply
the right `status:*` label among nine. The label hunt is the part that stalls — pick the wrong one
and the issue is silently parked, and nothing refuses. [#323](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/323)
replaces that second act with one: **clear the hold**.

The hold itself — the reserved label `hitl`, meaning *a person must act before anything else does* —
is being defined for the product by [#321](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/321)
(`hold-replaces-the-plan-gate`). That work governs `RunCreator`, which never sees a developer typing
`/aio:implement 323` in a terminal. Manual local invocation is the majority of how this repository is
actually built, and the command gate proposed here is the only thing covering it. The repository then
demonstrates, on itself, the mechanism the product ships.

**Source:** issue #323 — *A reviewer releases the loop by clearing one hold*. Main actor **ACT-001
Admin** (stated mapping: the actor catalogue describes Project roles; this item's actor is the repo
owner operating the repository's own loop, solo per DEC-003 — ACT-001 is the closest existing id and
no new actor is invented). Classification **Foundation**; governing capabilities `workflow-commands`
and `issue-lifecycle`. No `UC-*` — RULE-003 substitutes a foundation entry for a use case. No `BR-*`
— BR-001..016 govern the product's runtime, not repository process tooling. Depends on no open
decision (`OPN-*`), so RULE-006 does not block it.

## What Changes

- **`holdLabel` joins `.claude/workflow.json`** as the single home for the hold's name, alongside
  `wipLimit` and `lifecycleLabels`. No command and no document hardcodes `hitl`.
- **`read-issue` returns the hold** alongside the `status:*` label. It already fetches `labels` and
  today extracts only the status; the hold is an extraction, not a new fetch.
- **Three mutating commands refuse a held issue**: `/aio:propose` (before any branch or PR),
  `/aio:implement` (**before** the WIP gate, so a held issue consumes no slot), and `/aio:sync`
  (before merge, archive or retro). Each refusal names the hold and says who clears it.
- **Two commands are deliberately unaffected**: `/aio:grill` still evaluates and may comment but
  calls no `set-issue-status` — a hold blocks advancing, not talking; `/aio:refine` is post-merge and
  gates nothing. `/aio:status` reports the hold and refuses nothing (it is read-only).
- **Two commands apply the hold** at exactly the points a person must look: `/aio:propose` sets
  `status:ready-for-implementation` **and** the hold (the draft PR plus the hold *is* the spec-review
  stage); `/aio:implement` sets `status:code-review` **and** the hold.
- **Clearing the hold is the approval.** A reviewer who validates the spec removes one label and
  `/aio:implement` proceeds with no further label change. Nothing in this change ever clears a hold.
- **`docs/product/mvp/09-foundation-vs-product-split.md` gains a Foundation row** for the `/aio:*`
  workflow framework, so this item and future workflow-command items satisfy RULE-003/RULE-005.
- **BREAKING (process, not code):** `/aio:propose` no longer sets `status:proposal-review`, and the
  hand-off from spec review to implementation is now "remove the hold" rather than "set
  `status:ready-for-implementation`". No integration contract is affected — Aspire, host csproj, the
  outbox message schema and CI are untouched. Whether the now-unset `proposal-review` state should be
  retired from the nine is explicitly **out of scope** (see Impact).

## Capabilities

### New Capabilities

None. The hold is a property the existing commands honour, not a new capability of the framework.

### Modified Capabilities

- `workflow-commands`: adds the hold as a refusal condition on every mutating command and as an
  output of `propose` and `implement`; extends "tunable process values have one home" to cover
  `holdLabel`; states that `status` and `refine` are unaffected and that no command clears a hold.
- `issue-lifecycle`: states that the hold is **not** a `status:*` label — a held issue still carries
  exactly one of the nine — and that the hold, like the nine, is provisioned once and never invented
  by a command. Revises "two gates and two review stages" so the review stages are marked by the hold
  rather than by a state a person must set.

## Impact

**Affected surfaces (all documentation and command text — no application code):**

- `.claude/workflow.json` — new `holdLabel` key.
- `.claude/skills/read-issue/SKILL.md` — extract and return the hold.
- `.claude/commands/aio/propose.md`, `implement.md`, `sync.md` — hold refusal; `propose.md` and
  `implement.md` also apply it.
- `.claude/commands/aio/grill.md`, `status.md`, `refine.md` — state the hold's (non-)effect
  explicitly, so silence is never read as an oversight.
- `.claude/skills/set-issue-status/SKILL.md` — the transition table gains the hold's role and the
  rule that it is never a `status:*` value.
- `CONTRIBUTING.md`, `docs/process/` — the reviewer-facing description of the two review stages.
- `docs/product/mvp/09-foundation-vs-product-split.md` — the Foundation row (AC 14).
- `openspec/specs/workflow-commands/`, `openspec/specs/issue-lifecycle/` — via the delta specs here.

**Repository state this change does not create:** the `hitl` label does not currently exist on this
repository. Per `issue-lifecycle`'s "labels are provisioned once, not by automation", provisioning it
is a one-time human bootstrap step, recorded in tasks as such — no committed script creates it, and a
command that needs it and finds it missing stops and reports.

**Dependency on #321:** for the hold's *definition* only — its name and its meaning. The surfaces do
not overlap (`.claude/` and `docs/` here, `src/` there), so RULE-004 is satisfied and implementation
may proceed in parallel once each spec is validated.

**Explicitly out of scope:** retiring `status:proposal-review` and `status:code-review` from the nine
states; the product-side hold (#321); the Automations tab's board preview as an authoring surface;
any automation that *clears* a hold — clearing is a person's act, always; and renaming the label
per-repository — `hitl` is the same constant the product uses.
