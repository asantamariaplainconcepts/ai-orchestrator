# Tasks — ai-delivery-layer

Order follows doc 03's bootstrap sequence: instructions, then the authoring standard, then
telemetry, then skills, then the commands that compose them.

## 1. Instructions

- [x] 1.1 `AGENTS.md` written as a router: identity, where-things-live table, the loop with its
      gates, house rules. Links only; the not-yet-existing Phase 3 artifacts are listed with an
      explicit "*lands in bootstrap Phase 3*" marker rather than omitted or invented.
- [x] 1.2 Pointer files: `CLAUDE.md`, `.github/copilot-instructions.md`, `opencode.json`
      (`instructions: ["AGENTS.md"]`).
- [x] 1.3 Verify: all three name `AGENTS.md`; grep confirms zero references to
      `CONTRIBUTING.md`/`README.md` in any pointer.

## 2. Authoring standard

- [x] 2.1 `writing-great-skills` vendored under `.claude/skills/` with its `NOTICE`, extended
      with this repo's adaptation note (rename only, no substantive changes).
- [x] 2.2 Verify: NOTICE names Matt Pocock + MIT and sits beside the skill.

## 3. Telemetry

- [x] 3.1 `.config/otel/`: Collector config (file exporter with `append: true`,
      `project=ai-orchestrator` upserted server-side) + version-pinned compose
      (`otel/opentelemetry-collector-contrib:0.156.0`); optional Grafana LGTM compose +
      provisioned dashboard carried over.
- [x] 3.2 Hooks: `ensure-collector.mjs` (port probe, fail-soft start) and
      `map-session-change.mjs` (session→change JSONL append, worktree-safe, dedup). The data
      path deviates from the reference deliberately: `.telemetry/` (gitignored) instead of a
      committed folder — DEC-042/DEC-022, this repo is public.
- [x] 3.3 `.claude/settings.json`: OTLP export env + the two SessionStart hooks by absolute
      `$CLAUDE_PROJECT_DIR` path.
- [x] 3.4 Legacy env-tag hook (`tag-session-change.mjs`) dropped — it is the mechanism DEC-022
      records as broken. `/ds:propose`/`/ds:implement` lost their tag-write steps accordingly.
- [x] 3.5 Verify, executed for real: a fake session payload through `map-session-change.mjs`
      appended a correct record — and attributed this very branch to `change=ai-delivery-layer`;
      malformed stdin exits 0 quietly; `ensure-collector.mjs` fast-paths when :4317 is held;
      `git status` shows no telemetry file. **Local-machine caveat:** port 4317 on this machine
      is held by another project's collector (ds-connect), so cross-project sessions here can
      land in the wrong sink; the `upsert` project stamp keeps CLI sessions correct. One
      machine, one collector port — noted, not fixed here.
- [x] 3.6 Viewer evaluation (owner request): reviewed `claude-code-kanban`
      (NikiforovAll) as a lighter alternative to the Grafana stack. Finding: it is not
      OTel — user-scope shell hooks on lifecycle events writing JSONL under
      `~/.claude/.cck/` + a local SSE dashboard over `~/.claude` transcripts. It cannot
      replace the Collector (no token/cost metrics, no change attribution, feeds nothing
      to `collect-usage`), but it is a good **live activity viewer** without Grafana.
      Verdict: complementary, user-level opt-in, zero repo changes; the Grafana LGTM
      compose stays what it already was — optional. Nothing in this change depends on
      either viewer (the spec's "dashboards are disposable viewers" holds).

## 4. Skills

- [x] 4.1 GitHub-state: `create-github-issue`, `read-issue`, `set-issue-status`, `open-pr`,
      `mark-pr-ready` — lifecycle text retained (same nine labels), WIP reference now points at
      `.claude/workflow.json`, domain-specific phrasing generalized.
- [x] 4.2 Spec-engine: `openspec-propose`, `openspec-apply-change`, `openspec-archive-change`,
      `openspec-explore` + the four `/opsx:*` shims — copied as-is (grep-verified free of
      source-project references).
- [x] 4.3 Process: `grill-to-ready` (RULE ids remapped to this corpus's numbering: 001 fields /
      002 slicing / 003 traceability / 006 open decisions / 007 anti-patterns), `retro-entry`,
      `write-adr`, `rebase-safely`, `collect-usage` (paths → `.telemetry/`).
- [x] 4.4 Reviewed against `writing-great-skills`: descriptions lead with triggers, steps end on
      checkable done-whens, one responsibility each.
- [x] 4.5 Verify: no skill's steps invoke another skill (grep for invoke patterns — clean;
      "that's `other-skill`" mentions in Do-not sections are boundary statements, not calls);
      all four mutating skills carry a Confirm step; refusal paths in commands name the next
      command.

## 5. Commands

- [x] 5.1 `.claude/workflow.json`: `wipLimit: 2` (DEC-017), `squashBodyMaxLineLength: 100`, the
      nine lifecycle labels, the spec-less lane label. Commands read it; none hardcodes a value.
- [x] 5.2 Six `/ds:*` commands written with every spec'd gate: worktree preflight on all mutating
      commands; propose's fresh-base + branch-ends-with-slug + default-branch checks; implement's
      WIP gate (limit from workflow.json) + advisory overlap warning + in-progress-before-first-
      commit; sync's green-before-`[skip ci]` ordering, overlap re-check widened to `code-review`
      PRs, **squash subject/body commitlint gate**, explicit merge message, solo-path handling
      (DEC-016) and spec-less lane handling (DEC-025); refine append-only; status read-only.
      Adaptations from the reference: telemetry tag-write steps removed (attribution is
      automatic via the session hook), branch-protection guardrail rewritten for a public repo
      (verify rulesets with `gh api`, don't assume).
- [x] 5.3 Verify by dry-run against real state: no open issues → propose's read-issue gate
      refuses toward `/ds:grill`; `wipLimit` reads 2 from the single source with 0 in-progress;
      zero `status:*` labels exist → grill/set-issue-status fail loudly toward Phase 3 (by
      design); worktree preflight passes in this checkout. **The squash-lint gate was probed for
      real both ways:** a 140-char body line fails commitlint (exit 1), a wrapped body passes
      (exit 0) — the exact Phase 1 defect is now caught pre-merge.

## 6. What the E2E lane found during this change (post-implementation)

The lane went intermittently red on unchanged application code and stayed red across two
hypothesis-driven fixes; the third round of diagnostics named the real defect:

- **A kernel-level scoping bug.** `Sender` was a singleton, so it resolved handlers from the
  root provider and scoped `DbContext`s silently degraded to root-cached instances — one
  context shared across concurrent requests (`"a second operation was started on this context
  instance"`). Development's default `ValidateScopes` would have thrown at first resolution;
  E2E/production run without it, so only concurrent traffic (the browser test's SPA fetch
  racing the API test) could surface it. Fixed: `Sender` is scoped; scope validation is now
  **unconditional** in the host; a 16-parallel-reads functional test pins the regression.
- Along the way, three durable diagnostics improvements landed: non-production error bodies
  name the exception; the E2E fixture streams host logs keyed by runtime ResourceId (watching
  by declared name yields an empty stream) with a drain delay on failure; and the module's
  health check now includes its database (plus Npgsql retry-on-failure) — kept because health
  should mean "can serve requests", even though it was not the culprit here.
- Retro shape, again: the sequential test suite structurally could not see this bug. The E2E
  lane has now caught real defects on every first encounter — third time in two changes.

## 7. Close-out

- [x] 6.1 `AGENTS.md` lookup table carries the paths this change created (`.claude/workflow.json`,
      skills, commands, telemetry).
- [x] 6.2 Verify sweep above; CI on this PR runs lint + spec-validate (no application code — the
      code-less-skip gate applies).
- [x] 6.3 **Enforcement honesty (design D6), recorded:** every gate in the `/ds:*` commands is
      agent-enforced Markdown — worktree preflight, DoR gate, WIP cap, overlap checks, the
      green-before-close-out ordering, and the squash lint included. Machine-enforced remain:
      analyzers/ArchTests (build), Husky hooks (commit), CI lanes (PR), the draft-PR state
      (platform). The squash-lint gate is a strong candidate for future machine enforcement
      (e.g. a merge-queue check); until then it lives here and in the operator's discipline.
