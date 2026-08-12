---
name: "AIO: Implement"
description: For a validated proposal, implement on the same branch/PR and mark it ready for code review
category: Workflow
tags: [workflow, aio, openspec, implement]
---

Implement a validated change on the **same branch and PR** as its proposal, then mark that PR
ready for review (HITL #2). Wraps OpenSpec's apply.

**Input**: the GitHub issue number (or the change name). If omitted, ask.

**Steps**

1. **Worktree preflight.** Verify `git rev-parse --show-toplevel` matches the session's working
   directory; abort on mismatch before touching anything.
2. Invoke **`read-issue`**. **Gate:** if it is not `status:ready-for-implementation`, stop and
   report the actual status plus the next step (an unvalidated proposal needs its spec review;
   an ungrilled issue needs `/aio:grill <n>`). Do not proceed.
   - **Hold gate — runs *before* step 3.** If `read-issue` reports the hold (`holdLabel` in
     `.claude/workflow.json`), refuse now. Name the hold and say a person clears it by removing
     that one label. The ordering is normative, not stylistic: a held issue must consume no WIP
     slot and must never appear among the issues holding the cap, and a hold reported as a
     cap-refusal would send the reader to `/aio:sync` on an unrelated issue.
3. **WIP gate.** Read `wipLimit` from `.claude/workflow.json` (never hardcode it), then run
   `gh issue list --label status:in-progress --state open`. If the count has reached the limit,
   refuse to start: report the limit, list the issues holding it, and name `/aio:sync` as the way
   to free a slot.
   - **Branch-overlap warning (non-blocking):** once the count check passes, derive this change's
     declared touch-set (file paths named in its `tasks.md` and spec deltas) and, for each other
     open `status:in-progress` issue, resolve its branch (from its linked PR head ref) and compute
     `git diff --name-only <default>...<branch>`. If any file intersects, print a warning naming
     the overlapping file(s) and the other issue/branch. Advisory only — the operator may proceed.
     If a branch can't be resolved, skip it with a note rather than failing.
4. Invoke **`set-issue-status`** → `status:in-progress`, **before** any implementation commit.
5. Check out the change's existing branch (the one carrying the proposal PR). Do not create a new
   branch or a second PR.
6. Invoke the **`openspec-apply-change`** skill to work the change's tasks, committing
   incrementally so the branch keeps its narrative.
7. Push to the same branch (the existing PR updates automatically).
8. Invoke **`mark-pr-ready`** to flip the draft PR to ready-for-review.
9. Invoke **`set-issue-status`** → `status:code-review`, **and apply the hold in the same
   `gh issue edit`**. Removing that hold is what lets `/aio:sync` run.
10. Report the PR URL for code + observed-behaviour review, and say that the reviewer's whole act
    is **removing the hold** — no label to set.

**Guardrails**
- Never implement an unvalidated proposal.
- Never implement a **held** issue, and check the hold **before** the WIP gate — a held issue
  consumes no slot and is counted in no WIP tally.
- Never exceed the WIP limit — the gate runs before anything else except the hold, and the limit
  lives only in `.claude/workflow.json`.
- Never remove the hold. Clearing it is a person's act, always.
- The overlap warning is advisory; lifecycle gates and the hold never are.
- `status:in-progress` is set before the first implementation commit, not after.
- One PR per issue — never open a second PR.
- Reuse `openspec-apply-change`; keep commits meaningful (never squash locally — the branch is
  the post-mortem record).
- Gating shell steps set `pipefail` or check exit codes explicitly.
