# Definition of Ready

An issue is **ready** — eligible for `/aio:propose` and the `status:ready-for-proposal` label —
when it meets this bar.

This is not a parallel checklist. It **builds on**
[`docs/product/mvp/08-backlog-shaping-rules.md`](../product/mvp/08-backlog-shaping-rules.md),
which is the authority for issue shape; every requirement below cites the rule it enforces. When
a rule changes there, this document follows without being edited. `/aio:grill` drives a raw idea,
a use case from the corpus, or an existing issue to this bar.

## Required fields — [RULE-001](../product/mvp/08-backlog-shaping-rules.md)

- **Title** — capability-oriented and imperative: what the user can do afterwards, not the
  mechanism.
- **Value** — why this matters and to whom, in product terms. A value sentence naming a
  technology instead of an actor outcome is an anti-pattern ([RULE-007](../product/mvp/08-backlog-shaping-rules.md)).
- **Main actor** — an `ACT-*` id from [`01-actors-and-responsibilities.md`](../product/mvp/01-actors-and-responsibilities.md).
- **Priority** — relative to the current backlog.
- **Dependencies** — prerequisite issues, and any Foundation work that must land first.
- **Acceptance criteria** — deterministic given/when/then. Each must be evaluable to true or
  false by someone who did not write it; "works correctly" is not a criterion.
- **Business rules** — the `BR-*` ids the item must uphold ([`05-business-rules.md`](../product/mvp/05-business-rules.md)).
- **Affected use cases** — the `UC-*` ids it realises ([`04-mvp-use-cases.md`](../product/mvp/04-mvp-use-cases.md)).
- **Out of scope** — explicit exclusions, so the slice has an edge.
- **Change / spec ID** — the kebab-case slug that correlates issue → branch → PR → telemetry →
  retro. The branch name ends with it; attribution depends on it.

## Slicing — [RULE-002](../product/mvp/08-backlog-shaping-rules.md)

One capability per issue. If the acceptance criteria need an "and" between two behaviours, split
it. An item that cannot be implemented without first making an architectural decision is not a
slice — the decision becomes a Foundation item, and this one waits behind it.

## Traceability — [RULE-003](../product/mvp/08-backlog-shaping-rules.md)

A Product item cites at least one `UC-*`. A Foundation item cites the entry it enables in
[`09-foundation-vs-product-split.md`](../product/mvp/09-foundation-vs-product-split.md). Every
issue is classified Product or Foundation ([RULE-005](../product/mvp/08-backlog-shaping-rules.md)) —
enabling work is never smuggled inside a feature item.

## Sequencing — [RULE-004](../product/mvp/08-backlog-shaping-rules.md)

An item whose surface overlaps another in flight — `status:in-progress` **or** an open
`status:code-review` PR — is blocked behind it, not parallel to it. The second connector and the
second Agent runtime always sequence behind their first.

## Open decisions — [RULE-006](../product/mvp/08-backlog-shaping-rules.md)

Locked decisions (`DEC-*`) are binding. An item that depends on a **still-open** decision
([`07-open-decisions.md`](../product/mvp/07-open-decisions.md)) does **not** become ready: create
a blocking decision-closure item instead. Proposing on a guessed answer is the failure this gate
exists to prevent.

*Currently open and blocking:* `OPN-002` (Entra ID verification — blocks the auth slice and
UC-001). `OPN-003` closed with the Azure DevOps connector (DEC-045).

## Process gate

Beyond shape, an issue is ready to *start* only when its dependencies are themselves at least
ready, and any Foundation it relies on has landed.

## When an issue is not ready

Name the specific unmet fields, in a form that can be posted verbatim as a comment on the issue,
and set `status:needs-refinement`. A bare refusal is never acceptable — the reader must learn
what to fix without asking.
