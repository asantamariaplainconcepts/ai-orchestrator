---
name: "DS: Status"
description: Report where an issue/change sits in the loop and recommend the next /ds:* command
category: Workflow
tags: [workflow, ds, status]
---

Read-only. Diagnose lifecycle position and point to the next step.

**Input**: an issue number or change name. If omitted, infer from the current branch, else ask.

**Steps**

1. Invoke **`read-issue`** (and check the branch/PR state via `gh pr view` if relevant). Do not
   mutate anything.
2. Map to the lifecycle: `backlog → needs-refinement → ready-for-proposal → proposal-review →
   ready-for-implementation → in-progress → code-review → done` (or `blocked`).
3. Report the current status and the **next command**:
   - `backlog` / missing → `/ds:grill`
   - `needs-refinement` → resolve the commented DoR gaps, then `/ds:grill <n>` again
   - `ready-for-proposal` → `/ds:propose`
   - `proposal-review` → awaiting human validation of the spec (then set
     `ready-for-implementation`)
   - `ready-for-implementation` → `/ds:implement`
   - `in-progress` → implementation underway; `/ds:implement` finishes it out to `code-review`
   - `code-review` → awaiting code review, then `/ds:sync`
   - `done` → loop complete (the retro was captured in `/ds:sync`; `/ds:refine` for post-merge
     findings only)
   - `lane:spec-less` items skip `proposal-review`/`ready-for-implementation`: after grill they
     go straight to a branch + PR, then `code-review → done` via `/ds:sync` (no archive).
4. Note any drift (e.g. an open PR but the issue still `ready-for-proposal`, two `status:*`
   labels, a PR marked ready while its issue is `in-progress`).

**Guardrails**
- Never change GitHub state — this command only reports. Drift is reported, not corrected.
