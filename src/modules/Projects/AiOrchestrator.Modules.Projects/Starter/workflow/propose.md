---
name: "AIO: Propose"
description: For a ready issue, create the OpenSpec change on a branch and open a draft PR for spec review
category: Workflow
tags: [workflow, aio, openspec, propose]
---

Turn a `ready-for-proposal` issue into an OpenSpec proposal on a branch, opened as a **draft PR**
for human review (HITL #1). Wraps OpenSpec — the developer never calls `/opsx:*` directly.

**Input**: the GitHub issue number. If omitted, ask.

**Steps**

1. **Worktree preflight.** Verify `git rev-parse --show-toplevel` matches the session's working
   directory. If it doesn't, abort and report both paths — do not mutate anything.
2. Invoke **`read-issue`** for the number. **Gate:** if it is not `status:ready-for-proposal`,
   stop — do not proceed. Report the current status and the actionable next step: run
   `/aio:grill <n>` to close the Definition-of-Ready gaps.
   - **Hold gate.** If `read-issue` reports the hold (`holdLabel` in `.claude/workflow.json`),
     refuse here — before `git fetch`, before any branch, before any PR. Name the hold, say that a
     person clears it by removing that one label, and leave nothing behind.
3. **Fresh base.** `git fetch origin`, confirm the repository's real default branch
   (`gh repo view --json defaultBranchRef`), and create the branch from current
   `origin/<default>`. **The branch name must end with the change's kebab-case slug**
   (e.g. `change/<slug>`) — telemetry attribution and the overlap checks depend on it. If an
   existing branch for this change has a stale base, rebase it before continuing.
4. Invoke the **`openspec-propose`** skill to generate the OpenSpec change
   (proposal/design/specs/tasks), seeding the issue link and its UC/BR/actor IDs into the
   proposal's Why. Claims about infrastructure state go in only after being exercised for real —
   a config existing is not evidence it works.
5. Commit the change artifacts on the branch and push. (Session→change attribution is automatic:
   the SessionStart hook maps this branch to the change in `.telemetry/sessions.jsonl`.)
6. Invoke **`open-pr`** to open a **draft** PR (`Closes #<n>`, change name in the body), targeting
   the default branch confirmed in step 3.
7. Invoke **`set-issue-status`** → `status:ready-for-implementation`, **and apply the hold in the
   same `gh issue edit`**, so the issue is never briefly unheld in its new state. Do **not** set
   `status:proposal-review`: the draft PR plus the hold *is* the spec-review stage, and the next
   command's gating state is already in place for the moment the reviewer releases it.
8. Report the draft PR URL and **wait**: no code until a human validates the spec and **removes the
   hold**. Say so explicitly — clearing the hold is the approval, and the reviewer sets no label.

**Guardrails**
- Never propose an issue that isn't `status:ready-for-proposal` — on gate failure, point to
  `/aio:grill <n>`, never a bare refusal.
- Never propose a **held** issue. The hold gate runs before any git or GitHub mutation, so a
  refusal leaves no branch and no PR.
- The issue reads `status:ready-for-implementation` while its spec is still unreviewed — that is
  the design, and the hold is what makes it safe. The state says where the work is; the hold says
  nobody may take it further.
- Never remove the hold. Clearing it is a person's act, always.
- Never propose scope that depends on an open `OPN-*` decision.
- The PR is a draft on purpose (unmergeable) — it enforces the spec-review gate.
- The branch base must be current `origin/<default>` at creation; the branch name must end with
  the change slug. Both are hard requirements, not preferences.
- Reuse `openspec-propose`; do not re-implement artifact generation here.
- Any shell step whose exit code gates a decision runs with `pipefail` set or checks `$?`
  explicitly — a failure piped into another process must never read as success.
