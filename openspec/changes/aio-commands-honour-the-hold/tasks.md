## 1. Provision the hold and give it one home

- [ ] 1.1 **Human bootstrap act (not automated, D7/AC 2):** create the hold label once on the
  repository — `gh label create hitl --description "Held for a person — a command refuses while this
  is on" --color <colour>`. Verify with `gh label list | grep -i '^hitl'`. No committed script
  performs this; record it in `BOOTSTRAP-CHECKLIST.md` in task 4.4.
- [x] 1.2 Add `"holdLabel": "hitl"` to `.claude/workflow.json`, with a `$comment`-style note (matching
  the file's existing voice) that it is the reserved constant #321 defines and that it is **not** a
  lifecycle state, so it never joins `lifecycleLabels`.
- [x] 1.3 Verify AC 1 mechanically: `grep -rIn 'hitl' --exclude-dir=node_modules --exclude-dir=.git .`
  returns matches only in `.claude/workflow.json`, in this change's `openspec/` bundle, and in prose
  that quotes the issue — no command or skill file contains the literal.

## 2. Read the hold

- [x] 2.1 In `.claude/skills/read-issue/SKILL.md`, extend step 2 to extract the hold from the `labels`
  already fetched, comparing case-insensitively, resolving its name from `holdLabel` in
  `.claude/workflow.json` (AC 4, AC 3).
- [x] 2.2 In the same file, state that the returned result reports the hold as present **or absent** —
  never omitted — and that the skill stays read-only: it neither applies nor removes the hold.
- [x] 2.3 In `.claude/skills/set-issue-status/SKILL.md`, add to the lifecycle reference that the hold
  is not a `status:*` value and is never set or removed by a status transition; update the
  adjacent-transition table so `ready-for-proposal → ready-for-implementation` (via `/aio:propose`,
  with the hold applied in the same edit) and `in-progress → code-review` (via `/aio:implement`, with
  the hold applied in the same edit) replace the two `proposal-review` rows.

## 3. Refuse, and set, in the commands

- [x] 3.1 `.claude/commands/aio/propose.md`: add the hold check as the gate immediately after
  `read-issue`, before any git operation — refuse naming the hold and who clears it, creating no
  branch and no PR (AC 5).
- [x] 3.2 `.claude/commands/aio/propose.md`: change the final advance from
  `set-issue-status → status:proposal-review` to `status:ready-for-implementation` **plus the hold,
  applied in the same `gh issue edit`** (AC 11, D4). Update the closing report and the guardrails to
  say the draft PR plus the hold is the spec-review stage, and that clearing the hold is the approval
  (AC 12).
- [x] 3.3 `.claude/commands/aio/implement.md`: add the hold check as step 2.5 — after `read-issue`,
  **before** the WIP gate — so a held issue consumes no WIP slot and never appears among the issues
  holding the cap (AC 6, D3). Add a guardrail stating the ordering is normative.
- [x] 3.4 `.claude/commands/aio/implement.md`: change the final advance so `status:code-review` and
  the hold are applied in one edit when the PR is marked ready, and state that removing the hold is
  what lets `/aio:sync` run (AC 13).
- [x] 3.5 `.claude/commands/aio/sync.md`: add the hold check as the first gate in step 2, before the
  draft/rollup checks — refuse so nothing is merged, archived, or appended to the retro log (AC 7).
- [x] 3.6 `.claude/commands/aio/grill.md`: state that on a held issue it still evaluates and may
  comment, and calls no `set-issue-status`, reporting the hold as the reason the status did not
  advance (AC 8, D5).
- [x] 3.7 `.claude/commands/aio/status.md`: report the hold alongside the lifecycle position, name
  removing it as the next act and by whom, and state explicitly that status refuses nothing (AC 9).
  Update the lifecycle map so the two review stages read as holds.
- [x] 3.8 `.claude/commands/aio/refine.md`: state that the hold does not affect it — it is post-merge
  and gates nothing (AC 10, D5).
- [x] 3.9 Confirm no command or skill file removes the hold label anywhere:
  `grep -rn 'remove-label' .claude/` names only `status:*` labels.

## 4. Documentation and traceability

- [x] 4.1 `CONTRIBUTING.md`: update the lifecycle diagram and the stage table so both HITL stages are
  marked by the hold — HITL #1 at `ready-for-implementation` + hold, HITL #2 at `code-review` + hold
  — and so the reviewer's act reads as "remove the hold", not "set the next label". Refer to the hold
  by its role and to `holdLabel` for its value, never by the literal.
- [x] 4.2 `AGENTS.md`: update the lifecycle line to match.
- [x] 4.3 `docs/process/`: update whichever process document describes the two review stages so it
  agrees with `CONTRIBUTING.md`.
- [x] 4.4 `BOOTSTRAP-CHECKLIST.md` (and `BOOTSTRAP.md` where it lists label provisioning): add the
  one-time hold-label creation from task 1.1 alongside the nine `status:*` labels.
- [x] 4.5 `docs/product/mvp/09-foundation-vs-product-split.md`: add a Foundation row for the `/aio:*`
  workflow framework — what it enables and its notes — so this item and future workflow-command items
  satisfy RULE-003/RULE-005 (AC 14).

## 5. Mirror the starter catalogue

- [x] 5.1 Apply the task-3 edits to the six byte-identical copies under
  `src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/workflow/`
  (`propose.md`, `implement.md`, `sync.md`, `grill.md`, `status.md`, `refine.md`), per D6.
- [x] 5.2 Verify equality is restored: `diff` each pair against `.claude/commands/aio/` and expect no
  output for all six.
- [x] 5.3 Leave the manifest's `prerequisites` block unchanged, and carry Open Question 1
  (`.claude/workflow.json` as a prerequisite) to the reviewer rather than resolving it here.

## 6. Verification

- [x] 6.1 Re-run the AC 1 grep from task 1.3 after all edits — still only `workflow.json` and this
  change's bundle.
- [x] 6.2 Walk each of ACs 1–14 against the edited files and record the file:line that satisfies it,
  so the PR review has a checklist rather than a claim.
- [ ] 6.3 Dry-run the three refusals against a real held issue: label a scratch issue with the hold and
  confirm `/aio:propose`, `/aio:implement` and `/aio:sync` each refuse naming the hold, and that
  `/aio:implement`'s refusal cites the hold rather than the WIP cap even with the cap full. Remove
  the scratch label afterwards.
- [ ] 6.4 Confirm `/aio:status` and `/aio:grill` still run to completion on that held issue, and that
  `/aio:grill` set no `status:*` label.
- [x] 6.5 Run the repository's CI-equivalent gates for the touched surfaces:
  `rtk proxy pnpm --dir src/frontend lint` and `prettier --check` over the changed markdown as
  lint-staged would, plus `rtk proxy dotnet build` and
  `rtk proxy dotnet test src/tests/modules/Projects/AiOrchestrator.Modules.Projects.UnitTests` —
  the starter `.md` files are `EmbeddedResource`s, so `StarterCatalogue_Should_Constraint` covers
  them and must stay green.
- [x] 6.6 Confirm the commit messages pass commitlint (`docs:`/`feat:` per the surface touched) and
  that the pre-commit hook ran — `git log` shows the commits, not just an `ok`.
