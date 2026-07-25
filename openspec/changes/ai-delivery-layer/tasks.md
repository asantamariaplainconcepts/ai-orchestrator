# Tasks — ai-delivery-layer

Order follows doc 03's bootstrap sequence: instructions, then the authoring standard, then
telemetry, then skills, then the commands that compose them.

## 1. Instructions

- [ ] 1.1 Write `AGENTS.md`: what the project is, the where-things-live lookup table, the loop and
      its gates, short house rules. Links only — no restated specs, product facts, or decisions.
- [ ] 1.2 Pointer files for the three DEC-018 runtimes: `CLAUDE.md`,
      `.github/copilot-instructions.md`, `opencode.json`.
- [ ] 1.3 Verify: each pointer names `AGENTS.md`; grep the three files and confirm none points at
      `CONTRIBUTING.md` or `README.md`.

## 2. Authoring standard

- [ ] 2.1 Vendor `writing-great-skills` under `.claude/skills/` **with its `NOTICE`**, extending
      the notice with this repo's adaptations.
- [ ] 2.2 Verify: the NOTICE names Matt Pocock and the MIT licence, and sits beside the skill.

## 3. Telemetry

- [ ] 3.1 `.config/otel/`: Collector config (append-mode file exporter, `project=ai-orchestrator`
      stamped server-side by a resource processor) and version-pinned compose.
- [ ] 3.2 Session-start hooks (`ensure-collector.mjs`, `map-session-change.mjs`), invoked by
      absolute `$CLAUDE_PROJECT_DIR` path, fail-soft.
- [ ] 3.3 `.claude/settings.json` enabling the agent's OTLP export and wiring the hooks.
- [ ] 3.4 `.gitignore` the telemetry data paths; drop the legacy env-tagging hook — it is the
      mechanism DEC-022 records as broken, and shipping it invites its use.
- [ ] 3.5 Verify: start a session, confirm a line lands in `sessions.jsonl` with a session id;
      stop the Collector and confirm a session still starts cleanly; confirm `git status` shows
      no telemetry file.

## 4. Skills

- [ ] 4.1 GitHub-state: `create-github-issue`, `read-issue`, `set-issue-status`, `open-pr`,
      `mark-pr-ready` — relabelled to this project's lifecycle, each confirming before mutating.
- [ ] 4.2 Spec-engine: `openspec-propose`, `openspec-apply-change`, `openspec-archive-change`,
      `openspec-explore`, plus the thin `/opsx:*` command shims.
- [ ] 4.3 Process: `grill-to-ready` (reads the DoR document — Phase 3 writes it; reference by
      path), `retro-entry` (append-only), `write-adr` (allocates numbers against `origin/main`),
      `rebase-safely`, `collect-usage` (joins `usage.jsonl` with `sessions.jsonl` on session id).
- [ ] 4.4 Review every authored skill against `writing-great-skills`; prune no-ops and duplication.
- [ ] 4.5 Verify: no skill invokes another skill (grep for cross-references); every mutating skill
      has a confirmation step; every refusal names a next command.

## 5. Commands

- [ ] 5.1 `.claude/workflow.json` — the single home for tunables, starting with the WIP limit (2,
      DEC-017).
- [ ] 5.2 `/ds:grill`, `/ds:propose`, `/ds:implement`, `/ds:sync`, `/ds:refine`, `/ds:status`,
      each carrying its gates from the spec, including the worktree preflight, branch-slug and
      fresh-base checks, the WIP cap, the CI-green-before-`[skip ci]` ordering, the widened
      overlap check, and the squash-message lint.
- [ ] 5.3 Verify by dry-run against real repository state: `/ds:propose` on a non-ready issue
      refuses and names `/ds:grill`; an implement beyond the WIP cap refuses and names `/ds:sync`;
      `/ds:sync` on a red or draft PR refuses; a deliberately over-long squash body is rejected
      before any merge is attempted.

## 6. Close-out

- [ ] 6.1 Update `AGENTS.md`'s lookup table with the paths this change created.
- [ ] 6.2 Full verify sweep; CI green on the PR.
- [ ] 6.3 Note in the change's close-out which gates are agent-enforced (Markdown) rather than
      machine-enforced, per design D6 — so the distinction is recorded, not assumed.
