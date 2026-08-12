---
name: set-issue-status
description: Transition a GitHub issue's status label / Projects column to the next lifecycle state. Use when a command advances an issue in the loop.
---

Move one issue to a target `status:*` state — one responsibility.

## Lifecycle states (reference)

`status:backlog → status:needs-refinement → status:ready-for-proposal → status:proposal-review → status:ready-for-implementation → status:in-progress → status:code-review → status:done` (plus `status:blocked`, reachable from any state). Gates: `ready-for-proposal` unlocks `/aio:propose`; `ready-for-implementation` unlocks `/aio:implement` (subject to the WIP limit in `.claude/workflow.json`). See `CONTRIBUTING.md` for the full convention.

Adjacent-state transitions (the ones a command normally requests):
- `backlog → needs-refinement` or `backlog → ready-for-proposal` (via `/aio:grill`, depending on DoR outcome)
- `needs-refinement → ready-for-proposal` (via `/aio:grill`, once gaps are resolved)
- `ready-for-proposal → ready-for-implementation` (via `/aio:propose`, **with the hold applied in the same `gh issue edit`** — the draft PR plus the hold is the spec-review stage)
- `ready-for-implementation → in-progress` (via `/aio:implement`, before its first commit, once a human has removed the hold)
- `in-progress → code-review` (via `/aio:implement`, after push + PR marked ready, **with the hold applied in the same `gh issue edit`**)
- `code-review → done` (via `/aio:sync`, once a human has removed the hold, and only after merge + archive complete)
- any state `→ blocked` and back, on human decision

`status:proposal-review` is set by no command. It remains one of the nine and is left in place on any issue already carrying it; whether the lifecycle should shrink is a separate decision.

The `status:*` label is the sole lifecycle state — no automation touches GitHub Projects. Projects are label-filtered saved views only, so setting the label here is the whole operation; there is no board field to sync.

## The hold is not a status

The hold (`holdLabel` in `.claude/workflow.json`) says whether anyone may take the work further; the `status:*` label says where the work is. They answer different questions and are never conflated:

- The hold is **not** a `status:*` value, so no transition here ever sets or removes it as one.
- Where a command applies the hold, it travels in the **same** `gh issue edit` as the status change, so the issue is never briefly unheld in its new state.
- Removing the hold is a **person's** act. This skill never does it, and neither does any other command.

## Steps

1. **Confirm.** State the issue, its current status, and the target status. Proceed only on confirmation — this mutates shared GitHub state.
   - Done when: the human approves the transition.
2. **Apply.** Remove the old `status:*` label and add the target one (`gh issue edit <n> --remove-label … --add-label …`).
   - Done when: the issue carries exactly one `status:*` label matching the target.
3. **Report.** Return the new status for the command's next step.
   - Done when: new status handed back.

## Do not

- Skip a gate (e.g. jump `ready-for-proposal` → `code-review`) — only advance to the adjacent state the command owns.
- Leave two `status:*` labels on an issue.
- Touch the GitHub Project board here — it's an unsynchronized, label-filtered view; only the `status:*` label matters.
