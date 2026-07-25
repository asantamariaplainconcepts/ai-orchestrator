---
name: "DS: Sync"
description: Close out an approved change on its branch (retro + archive + sync), lint the squash message, then squash-merge one commit to main
category: Workflow
tags: [workflow, ds, openspec, sync, archive, retro]
---

Close out an approved change and land it. The retro, the archive, and the spec sync all happen
**on the branch, before the merge**, so the squash-merge puts a **single commit** on `main`
containing the implementation, the retro entry, the synced `openspec/specs/`, and the archived
change. **Merge = archive = sync = retro**, one commit.

**Input**: the GitHub issue number (or the change name). If omitted, ask.

**Steps**

1. **Worktree preflight.** Verify `git rev-parse --show-toplevel` matches the session's working
   directory; abort on mismatch.
2. Invoke **`read-issue`**. **Gate:** the issue must be `status:code-review` and the PR must not
   be a draft; refuse otherwise and say what's missing. **Solo path (DEC-016):** GitHub forbids
   self-approval, so do not gate on a formal PR approval — the recorded review is the human's
   explicit go-ahead in this session plus the PR checklist; state that and get the go-ahead
   before continuing.
   - **Spec-less lane (DEC-025):** if the issue carries `lane:spec-less`, skip steps 5 (archive)
     — there is no change bundle — but every other step, including the retro and the
     squash-message lint, still applies.
3. **Bring the branch up to date with `main`.** Merge/rebase current `origin/main` into the
   change branch, so the archive folds delta specs into up-to-date `openspec/specs/`. Sequential
   syncs stay conflict-free only because each runs against already-synced specs.
   - **Overlap re-check (advisory):** re-run the branch-footprint overlap check here, against
     both `status:in-progress` issues **and other open `status:code-review` PRs** — a collision
     discovered at sync is cheaper than one discovered as a merge conflict on `main`.
4. **Capture the retro — before the merge, so it rides in the same commit.**
   1. Invoke **`collect-usage`** for the change (joins `.telemetry/usage.jsonl` with
      `.telemetry/sessions.jsonl` on `session.id`).
   2. **Propose** the three reflection points — what worked, what didn't, one change next time —
      drafted from the change's actual history (commits, blockers, CI findings, telemetry), and
      present them for the human to confirm or edit. Lead with a draft, not a cold question.
   3. Invoke **`retro-entry`** to append the entry to `docs/process/retro-log.md`, stating the
      time source (telemetry or manual).
   4. If a reflection point is structural — anything recurring, or changing how the workflow
      behaves — invoke **`write-adr`** (it allocates the next number against `origin/main`) and
      link it from the entry. The graduation rule is the **second** occurrence.
5. **Archive + sync on the branch.** Invoke **`openspec-archive-change`**: fold the delta specs
   into `openspec/specs/` and move the bundle to `openspec/changes/archive/YYYY-MM-DD-<name>/`.
   Verify exactly one archive directory exists for this change afterwards.
6. **Precondition — gate on CI green BEFORE writing the close-out commit.** This is the only
   point where the check rollup reliably reflects the code: the current PR head is still the last
   **implementation** commit, so its rollup is authoritative. Run
   `gh pr view --json isDraft,statusCheckRollup`. If the PR is a draft, or the rollup is red or
   genuinely pending, **refuse — and do NOT create the close-out commit.** This ordering is
   load-bearing: once step 7 pushes the `[skip ci]` commit, that head SHA has no check runs at
   all, so its empty rollup can no longer prove greenness — and an empty rollup reads as
   "nothing failing".
7. **One close-out commit — marked `[skip ci]`.** Only after step 6 is green: commit the retro
   entry + synced specs + archived change together and push. Subject:
   `chore(openspec): close out <name> — retro + archive + sync [skip ci]`. The marker suppresses
   a redundant full re-run on an already-verified PR (paths-filter diffs the whole PR, which
   still contains code). The push deliberately triggers **no** new run — do not wait for one,
   and never re-inspect this commit's empty rollup as evidence of anything.
8. **Lint the squash message, then refresh the title.** Inspect the current PR title
   (`gh pr view --json title`) and **always present it to the human with a verdict** — never
   decide "already accurate" silently. If it still matches the proposal-time pattern
   (`docs(openspec): propose …`), derive a conventional-commit subject describing the
   **implemented** change (issue reference `(#<issue>)`; never the PR number — GitHub appends
   that itself) and have it confirmed or edited. Compose the squash **body** explicitly: a clean
   summary, wrapped so **no line exceeds `squashBodyMaxLineLength` from `.claude/workflow.json`**,
   containing **no** CI-skip token. **Gate:** validate the exact subject and body against
   commitlint before merging — e.g. write them to a temp file and run
   `pnpm exec commitlint --config ../../.config/commitlint.config.js` from `src/frontend` (set
   `pipefail`). If it fails, fix the message and re-lint; **do not merge with an unvalidated
   message.** This is the gate Phase 1 proved cannot live anywhere else: no local hook ever sees
   a squash message, and the CI check runs only after the merge is irreversible.
9. **Squash-merge, setting the message explicitly.** Apply any new title with
   `gh pr edit --title` (a title edit does not re-trigger CI), then immediately
   `gh pr merge --squash --subject "<linted subject>" --body "<linted body>"`. **Never let GitHub
   auto-generate the squash body** — the default concatenates branch commit messages, including
   the step-7 `[skip ci]` subject, and GitHub scans subject **and** body, so an auto-body would
   suppress the main-push pipeline. `main` gets one clean commit; subject = the PR title.
10. Only once the merge has completed, invoke **`set-issue-status`** → `status:done`.

**Guardrails**
- **Gate CI green *before* the close-out commit, never after** (step 6). Refuse on a draft, a red
  rollup, or a pending check. The `[skip ci]` commit's empty rollup proves nothing; never read it
  as green, and never wait for a run its push deliberately didn't trigger.
- **The squash subject and body are linted before the merge** (step 8) — every artifact that will
  become `main`'s history is validated while the merge is still preventable, never after.
- **`[skip ci]` must never reach `main`** — step 9 sets the squash message explicitly; never rely
  on GitHub's default body, and never put the marker in a PR title.
- **Branch protection interaction:** this repo is public (free-plan rulesets available). If a
  required-status-check rule is ever enabled on `main`, revisit steps 6–7: the `[skip ci]`
  close-out commit has no check runs, and a required check with no run would deadlock the merge.
  Verify the actual protection state with `gh api repos/{owner}/{repo}/rulesets` — do not assume.
- `/ds:sync` is the **sole owner** of the accurate-subject guarantee: never squash-merge while
  the title still describes the proposal. Whatever set the title upstream, verify it here.
- Never set `status:done` before the merge has actually completed.
- Retro time comes from `collect-usage`; if telemetry is missing, the entry says so (manual).
  The retro log is append-only.
- A genuinely post-merge finding is appended afterwards with `/ds:refine` — never by rewriting
  this change's entry.
- Archive on the branch before merging, exactly one archive directory per change. Merges are
  sequential — step 3 always completes before the archive.
- Do not squash the branch locally; the branch is the post-mortem record. The squash happens only
  at the GitHub merge.
- Gating shell steps set `pipefail` or check exit codes explicitly.
