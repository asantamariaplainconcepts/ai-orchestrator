---
name: "AIO: Ship"
description: Carry a ready issue all the way to a squash-merge on main in one unattended run — no review stage
category: Workflow
tags: [workflow, aio, openspec, ship, unattended]
---

Carry a `ready-for-proposal` issue to `main` in **one unattended run**: propose, implement, sync, with
no human review stage between them. **Your invocation is the approval** — it replaces DEC-016's
in-session go-ahead, and it is the only thing authorising the merge (DEC-068,
[ADR-0027](../../../docs/adr/0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)).

This command **owns no gates**. It runs the three staged commands in their **unattended mode** and
states only what that mode changes; every precondition, ordering and guarantee lives in those files
and is inherited by invoking them, never by being restated here (ADR-0003). Read them for the
mechanics: [`propose.md`](propose.md), [`implement.md`](implement.md), [`sync.md`](sync.md).

**Input**: the GitHub issue number. If omitted, ask.

**Steps**

1. **Say what is about to happen, then proceed.** State plainly: this run will merge to `main` with
   nobody reading the spec or the diff, and the only automated reviewer is CI. Do **not** ask for
   confirmation — the invocation already gave it, and asking would rebuild the gate this route
   removes.
2. Invoke **[`/aio:propose`](propose.md) in unattended mode**. Its step-2 gates are this command's
   entry gates: a wrong status or a held issue refuses there, before any mutation, and nothing is
   left behind.
3. Invoke **[`/aio:implement`](implement.md) in unattended mode** on the branch and PR propose left.
4. Invoke **[`/aio:sync`](sync.md) in unattended mode**, which merges and reports the deploy.
5. Report the outcome: the merge commit, the issue at `status:done`, and the deploy's conclusion —
   naming the failing step if it is red. A red deploy on a merged change is reported, never softened.

**Halting** — the same contract in all three stages, and the only thing this command adds to them:

- **Stop, do not guess.** Halt on a failing or pending check rollup, on the WIP cap, and on any
  question the issue and its spec do not answer. Nothing is retried (BR-004's instinct).
- **A halt applies the hold**, comments the specific reason on the issue, and leaves the `status:*`
  label exactly as it is. Applying the hold is permitted; **removing one never is.**
- **Hand back cleanly.** A halted change is resumed by a person clearing the hold and running the
  ordinary staged command for the issue's current label — no repair step, no second branch, no second
  PR, no second archive directory.

**The record** — an unattended change must be legible as one afterwards:

- The **PR body** states that the change landed with no human reading its spec or its diff, and names
  `/aio:ship`.
- The **retro entry** says the same, and marks its three reflection points **unconfirmed** — nobody
  confirmed them.
- Without those two marks the retro log cannot tell the routes apart, and no future claim about either
  is measurable (ADR-0018). They are part of the decision, not decoration.

**Guardrails**
- **Never remove the hold.** Not on a halt, not at the end, not ever. Clearing it is a person's act,
  always — that invariant is what makes the hold worth trusting on the reviewed path, and this route
  exists precisely because it never needs one.
- **Never widen a staged command's gate.** Unattended mode suppresses waits and questions; it never
  softens the status gates, the WIP cap, the CI-green precondition and its ordering, the commitlint
  gate, the worktree assertion, or `[skip ci]` never reaching `main`. A run that cannot pass a gate
  halts — it does not proceed.
- **Never restate a staged command's steps here.** If this file starts explaining sync's ordering, the
  ordering has two owners and will drift (#202 is what that costs). Add the rule to the staged command
  and let this one inherit it.
- **Never ask for confirmation mid-run**, and never treat silence as one. The route is authorised at
  invocation or not at all.
- The staged route stays the default. This one is for work whose shape a person already accepted at
  the grill.
