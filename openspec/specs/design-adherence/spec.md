# design-adherence Specification

## Purpose
TBD - created by archiving change design-system. Update Purpose after archive.
## Requirements
### Requirement: one validator, three stages, in the lint lane

A single script SHALL enforce the design system in three stages — adherence, drift, skill
hygiene — and SHALL run as a step of the existing CI lint lane on every pull request. It SHALL be
runnable locally with the same command and produce the same verdict.

#### Scenario: same result locally and in CI

- **WHEN** a contributor runs the validator locally on a clean tree
- **THEN** it agrees with what CI will report

### Requirement: adherence — frontend code uses tokens, not literals

Stage 1 SHALL fail when frontend source contains a raw hex colour, a raw pixel value where a
spacing/radius/type token exists, a font family outside the approved stack, or hardcoded
user-facing JSX copy. The existing i18n rule SHALL be this stage's copy check rather than a
parallel one.

#### Scenario: a raw hex

- **WHEN** a component styles with `#1a1a1a`
- **THEN** the lint lane fails and names the file and the token to use instead

#### Scenario: hardcoded copy

- **WHEN** a component renders literal user-facing text
- **THEN** the lint lane fails, exactly as it does today

#### Scenario: false positives are fixed at the pattern

- **WHEN** the checker flags something that is not a violation
- **THEN** the pattern is narrowed; the rule is not downgraded to advisory

### Requirement: drift — generated layers must match canonical

Stage 2 SHALL run the generator in `--check` mode and fail when `DESIGN.md` or the runtime adapter
does not match the canonical tokens.

#### Scenario: canonical edited without regenerating

- **WHEN** a token changes in `docs/design-system/` and the generated files are not refreshed
- **THEN** the lint lane fails and names the regeneration command

### Requirement: skill hygiene — the router stays value-free

Stage 3 SHALL fail when the design skill contains literal token values.

#### Scenario: a value pasted into the skill

- **WHEN** a colour or size literal is added to the design skill
- **THEN** the lint lane fails, because a skill that carries values can drift from the canonical
  layer

### Requirement: the gate is scoped to the artifacts it was written against

Each rule SHALL apply only to the artifact shape it was authored for. Generated files, vendored
third-party assets, and the canonical design system itself SHALL be excluded from the adherence
patterns, which target application source.

#### Scenario: the canonical tokens are not flagged

- **WHEN** the validator runs
- **THEN** `docs/design-system/` is not reported for containing colour values — that is its job

