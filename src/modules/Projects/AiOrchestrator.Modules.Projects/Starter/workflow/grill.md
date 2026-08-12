---
name: "AIO: Grill"
description: Interrogate an idea or a markdown source to Definition of Ready, then create the GitHub issue
category: Workflow
tags: [workflow, aio, issue, grill]
---

Take a raw input, or an existing issue, to `status:ready-for-proposal`. This is the public entry
point; it orchestrates skills and does not itself talk to OpenSpec.

**Input**: an inline idea, a path/section (e.g. `docs/product/v1/04-capabilities.md#UC-012`),
or an existing GitHub issue number. If omitted, ask what to grill.

**Steps — raw input (new issue)**

1. Invoke the **`grill-to-ready`** skill with the input. It reads the source + product context
   (glossary, business rules, locked decisions) and interrogates the human until the Definition
   of Ready (`docs/process/definition-of-ready.md`) is met, emitting an issue draft. If that
   document does not exist yet, stop and say so — do not improvise a rubric.
2. Review the draft together. If an open decision (`OPN-*` in
   `docs/product/mvp/07-open-decisions.md`) blocks it, keep it as a blocking decision-closure
   item — never propose on a guess (RULE-006).
3. Invoke **`create-github-issue`** with the approved draft (it confirms before creating).
4. Invoke **`set-issue-status`** to set the new issue to `status:ready-for-proposal`. If the
   label does not exist in the repository, stop and report that the lifecycle labels have not
   been created yet (bootstrap Phase 3) — never create labels ad hoc.
5. Report the issue number/URL and that it's ready for `/aio:propose`.

**Steps — existing issue number**

1. Invoke **`read-issue`** for the number.
2. Invoke **`grill-to-ready`** in existing-issue mode, passing the issue's current title/body: it
   evaluates against the Definition of Ready and returns either the specific missing fields, or a
   readiness confirmation.
3. **If unmet**: post a comment on the issue (`gh issue comment <n> --body "..."`) listing the
   specific missing DoR fields from step 2, then invoke **`set-issue-status`** to set/keep
   `status:needs-refinement`. Stop — do not advance until the gaps are resolved.
4. **If met**: invoke **`set-issue-status`** to move the issue to `status:ready-for-proposal`.
5. Report the outcome and, if advanced, that it's ready for `/aio:propose`.

**If the issue carries the hold** (`holdLabel` in `.claude/workflow.json`): still evaluate, and
still comment the gaps. Skip steps 3 and 4 — invoke **no** `set-issue-status` — and report the hold
as the reason the status did not advance. A hold blocks advancing, not talking: the gap check is
exactly what a reviewer wants while deciding whether to clear it.

**Guardrails**
- Do not create a new issue, or advance an existing one to `status:ready-for-proposal`, until the
  Definition of Ready is satisfied.
- An item depending on an open `OPN-*` decision is blocked behind a decision-closure task —
  refuse to mark it ready and name the blocking decision.
- Every state-changing skill confirms before mutating GitHub.
- Issues seeded in bulk from `docs/product/v1/` still pass through this grill individually.
- A DoR-gap comment names the specific missing fields — never a bare rejection.
- A hold stops grill from **advancing**, never from evaluating or commenting — and grill never
  removes it. Clearing the hold is a person's act, always.
- A hotfix or pure infra/tooling item may take the spec-less lane instead (DEC-025): label it
  `lane:spec-less` here, and it skips `/aio:propose` — but it still needs an issue, a branch, a
  PR, CI, and a retro entry at sync.
