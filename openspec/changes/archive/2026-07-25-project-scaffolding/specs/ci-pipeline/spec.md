# ci-pipeline

## ADDED Requirements

### Requirement: one orchestrator, reusable stages

CI SHALL be one orchestrator workflow (`.github/workflows/ci.yml`) calling per-stage
reusable workflows: lint, build-test, e2e (local emulator mode), spec-validate. A
`changes` gate job using `dorny/paths-filter` with **job-level `if`** SHALL scope
lanes to the diff; `on.paths` SHALL NOT be used (required checks must always report).

#### Scenario: docs-only PR stays light

- **WHEN** a PR touches only `docs/**` or `openspec/**`
- **THEN** lint and spec-validate run; build-test and e2e are skipped yet report as
  successful required checks

### Requirement: lint lane mirrors the hooks

The lint lane SHALL run CSharpier `--check`, Prettier `--check`, ESLint
`--max-warnings=0`, `tsc --noEmit`, and commitlint over the PR's commits — the same
gates as the local hooks, so bypassing hooks locally cannot land.

#### Scenario: hook-equivalent failure

- **WHEN** an unformatted file or malformed commit message reaches a PR
- **THEN** the lint lane fails

### Requirement: spec validation gate

Any PR touching `openspec/**` SHALL run `npx @fission-ai/openspec@1.6.0 validate
--changes` (package name and version pinned — the bare `openspec` npm package does
not exist).

#### Scenario: invalid delta blocks merge

- **WHEN** a change bundle with a malformed delta spec is pushed
- **THEN** the spec-validate check fails

### Requirement: artifacts on failure only

Workflow artifact uploads (test results, Playwright traces/screenshots) SHALL use
`if: failure()` — a shared artifact-quota hit must never red a green run.

#### Scenario: green run uploads nothing

- **WHEN** all jobs pass
- **THEN** no artifacts are uploaded
