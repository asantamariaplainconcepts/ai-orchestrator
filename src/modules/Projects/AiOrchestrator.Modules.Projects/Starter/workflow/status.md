---
name: "AIO: Status"
description: Report where an issue/change sits in the loop and recommend the next /aio:* command
category: Workflow
tags: [workflow, aio, status]
---

Read-only. Diagnose lifecycle position and point to the next step.

**Input**: an issue number or change name. If omitted, infer from the current branch, else ask.

**Steps**

1. Invoke **`read-issue`** (and check the branch/PR state via `gh pr view` if relevant). Do not
   mutate anything.
2. Map to the lifecycle: `backlog → needs-refinement → ready-for-proposal → proposal-review →
   ready-for-implementation → in-progress → code-review → done` (or `blocked`).
3. Report the current status and the **next command**:
   - `backlog` / missing → `/aio:grill`
   - `needs-refinement` → resolve the commented DoR gaps, then `/aio:grill <n>` again
   - `ready-for-proposal` → `/aio:propose`
   - `ready-for-implementation` **+ hold** → HITL #1: awaiting human validation of the spec on the
     draft PR. The reviewer's whole act is **removing the hold** — no label to set.
   - `ready-for-implementation`, no hold → `/aio:implement`
   - `in-progress` → implementation underway; `/aio:implement` finishes it out to `code-review`
   - `code-review` **+ hold** → HITL #2: awaiting code review on the ready PR. Again, removing the
     hold is the whole act.
   - `code-review`, no hold → `/aio:sync`
   - `proposal-review` → set by no command since the hold replaced it; report it as a legacy state
     and name the hold as what marks spec review now
   - `done` → loop complete (the retro was captured in `/aio:sync`; `/aio:refine` for post-merge
     findings only)
   - `lane:spec-less` items skip `proposal-review`/`ready-for-implementation`: after grill they
     go straight to a branch + PR, then `code-review → done` via `/aio:sync` (no archive).
4. **Report the hold first.** If `read-issue` reports the hold (`holdLabel` in
   `.claude/workflow.json`), lead with it: the issue is held, removing that one label is the next
   act, and it is the reviewer's to perform. A reader who arrives here is usually a reviewer
   wondering why a command refused — the hold is the answer.
5. Note any drift (e.g. an open PR but the issue still `ready-for-proposal`, two `status:*`
   labels, a PR marked ready while its issue is `in-progress`).

**Guardrails**
- Never change GitHub state — this command only reports. Drift is reported, not corrected.
- **Refuse nothing.** A hold is a fact this command reports, never a gate it obeys — status is
  read-only, and reporting the hold is precisely what a stalled reviewer needs.
- Never remove the hold. Clearing it is a person's act, always.
