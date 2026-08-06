# Definition of Ready

> **This is a starter, and it is now yours.** It was installed because the spec-first workflow's
> prompts read a readiness bar and this repository had none. Edit it, cut it down, or replace it —
> the prompts read *this file*, so what it says is what your team is held to. Nothing re-installs or
> overwrites it.

An issue is **ready** — eligible for a spec proposal — when it meets this bar.

This document is not a parallel checklist. It **builds on**
[`backlog-shaping-rules.md`](./backlog-shaping-rules.md), which is the authority for issue shape;
every requirement below cites the rule it enforces. When a rule changes there, this document follows
without being edited.

## Required fields — RULE-001

- **Title** — capability-oriented and imperative: what the user can do afterwards, not the mechanism.
- **Value** — why this matters and to whom, in product terms. A value sentence naming a technology
  instead of an actor outcome is an anti-pattern (RULE-007).
- **Main actor** — an actor id from your product context.
- **Priority** — relative to the current backlog.
- **Dependencies** — prerequisite issues, and any enabling work that must land first.
- **Acceptance criteria** — deterministic given/when/then. Each must be evaluable to true or false by
  someone who did not write it; "works correctly" is not a criterion.
- **Business rules** — the rule ids the item must uphold.
- **Affected use cases** — the use-case ids it realises.
- **Out of scope** — explicit exclusions, so the slice has an edge.
- **Change / spec ID** — the kebab-case slug that correlates issue → branch → PR → retro. The branch
  name ends with it.

Define the actor, use-case, business-rule and decision id conventions in
[`product-context.md`](./product-context.md). The ids are yours; only the discipline of citing them
comes from here.

## Slicing — RULE-002

One capability per issue. If the acceptance criteria need an "and" between two behaviours, split it.
An item that cannot be implemented without first making an architectural decision is not a slice —
the decision becomes its own item, and this one waits behind it.

## Traceability — RULE-003

A product item cites at least one use case. An enabling item cites what it unblocks. Every issue is
classified as one or the other (RULE-005) — enabling work is never smuggled inside a feature item.

## Sequencing — RULE-004

An item whose surface overlaps another already in flight is blocked behind it, not parallel to it.

## Open decisions — RULE-006

Recorded decisions are binding. An item that depends on a **still-open** question does **not** become
ready: create a decision-closure item instead. Proposing on a guessed answer is the failure this gate
exists to prevent.

## Process gate

Beyond shape, an issue is ready to *start* only when its dependencies are themselves at least ready,
and any enabling work it relies on has landed.

## When an issue is not ready

Name the specific unmet fields, in a form that can be posted verbatim as a comment on the issue. A
bare refusal is never acceptable — the reader must learn what to fix without asking.
