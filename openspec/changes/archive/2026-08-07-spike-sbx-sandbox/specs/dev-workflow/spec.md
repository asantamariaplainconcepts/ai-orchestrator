# dev-workflow — deltas

## ADDED Requirements

### Requirement: a spike is a change, and its verdict is evidence

An investigation whose deliverable is a decision rather than product behaviour (a spike) SHALL
be captured as an OpenSpec change: hypotheses in the proposal, method and rejected alternatives
in the design, verdicts in a `findings.md` inside the change directory. Each verdict SHALL
carry the command exercised and the observed output (ADR-0001: verify claims by exercising
them); a hypothesis that was not exercised SHALL read **not verified**, never be inferred from
documentation. Spike artifacts — harnesses, configuration, timings — SHALL live inside the
change directory, never under `src/`, and a "go" verdict SHALL name the follow-up change it
licenses.

#### Scenario: a spike concludes

- **WHEN** a spike's tasks complete
- **THEN** `findings.md` records a per-hypothesis verdict with its exercised evidence, and a
  "go" names the follow-up change

#### Scenario: a spike harness drifts toward the product

- **WHEN** a spike's harness or configuration would land under `src/`
- **THEN** it is moved into the change directory, or the work stops being a spike and gets a
  real proposal
