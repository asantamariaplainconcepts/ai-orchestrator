# dev-workflow Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
### Requirement: hooks install themselves

Git hooks SHALL live in `.husky/` and install via a `Directory.Build.props` restore
target (`git config core.hooksPath .husky` on first non-CI build). A "forgot to
install hooks" state SHALL NOT exist for anyone who has built the solution.

#### Scenario: fresh clone gets hooks

- **WHEN** a fresh clone runs `dotnet build` once
- **THEN** subsequent commits run the pre-commit and commit-msg hooks

### Requirement: pre-commit formats and lints staged files

The pre-commit hook SHALL run CSharpier on staged C# files and lint-staged
Prettier/ESLint on staged frontend files — staged files only, never the whole tree.

#### Scenario: unformatted C# is fixed or rejected at commit

- **WHEN** a commit stages an unformatted `.cs` file
- **THEN** the hook formats (or fails) before the commit is created

### Requirement: Conventional Commits enforced

The commit-msg hook SHALL run commitlint with the config at
`.config/commitlint.config.js`; CI SHALL re-run commitlint over PR commits so
`--no-verify` is still caught.

#### Scenario: bad message rejected locally

- **WHEN** `git commit -m "fixed stuff"` runs
- **THEN** the commit is rejected with the commitlint report

#### Scenario: --no-verify caught in CI

- **WHEN** a commit bypasses hooks locally and reaches a PR
- **THEN** the CI lint lane fails on the malformed message

### Requirement: a spike is a change, and its verdict is evidence

An investigation whose deliverable is a decision rather than product behaviour (a
spike) SHALL be captured as an OpenSpec change: hypotheses in the proposal, method
and rejected alternatives in the design, verdicts in a `findings.md` inside the
change directory. Each verdict SHALL carry the command exercised and the observed
output (ADR-0001: verify claims by exercising them); a hypothesis that was not
exercised SHALL read **not verified**, never be inferred from documentation. Spike
artifacts — harnesses, configuration, timings — SHALL live inside the change
directory, never under `src/`, and a "go" verdict SHALL name the follow-up change
it licenses.

#### Scenario: a spike concludes

- **WHEN** a spike's tasks complete
- **THEN** `findings.md` records a per-hypothesis verdict with its exercised
  evidence, and a "go" names the follow-up change

#### Scenario: a spike harness drifts toward the product

- **WHEN** a spike's harness or configuration would land under `src/`
- **THEN** it is moved into the change directory, or the work stops being a spike
  and gets a real proposal

