---
name: "DS: Propose"
description: For a ready issue, create the OpenSpec change on a branch and open a draft PR for spec review
category: Workflow
tags: [workflow, ds, openspec, propose]
---

Turn a `ready-for-proposal` issue into an OpenSpec proposal on a branch, opened as a **draft PR**
for human review (HITL #1). Wraps OpenSpec — the developer never calls `/opsx:*` directly.

**Input**: the GitHub issue number. If omitted, ask.

**Steps**

1. **Worktree preflight.** Verify `git rev-parse --show-toplevel` matches the session's working
   directory. If it doesn't, abort and report both paths — do not mutate anything.
2. Invoke **`read-issue`** for the number. **Gate:** if it is not `status:ready-for-proposal`,
   stop — do not proceed. Report the current status and the actionable next step: run
   `/ds:grill <n>` to close the Definition-of-Ready gaps.
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
7. Invoke **`set-issue-status`** → `status:proposal-review`.
8. Report the draft PR URL and **wait**: no code until a human validates the spec and moves the
   issue to `status:ready-for-implementation`.

**Guardrails**
- Never propose an issue that isn't `status:ready-for-proposal` — on gate failure, point to
  `/ds:grill <n>`, never a bare refusal.
- Never propose scope that depends on an open `OPN-*` decision.
- The PR is a draft on purpose (unmergeable) — it enforces the spec-review gate.
- The branch base must be current `origin/<default>` at creation; the branch name must end with
  the change slug. Both are hard requirements, not preferences.
- Reuse `openspec-propose`; do not re-implement artifact generation here.
- Any shell step whose exit code gates a decision runs with `pipefail` set or checks `$?`
  explicitly — a failure piped into another process must never read as success.
