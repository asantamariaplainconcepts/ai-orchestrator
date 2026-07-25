# definition-of-ready Specification

## Purpose
TBD - created by archiving change ceremonies. Update Purpose after archive.
## Requirements
### Requirement: the Definition of Ready cites the backlog rules rather than restating them

`docs/process/definition-of-ready.md` SHALL define the bar an issue must meet to reach
`status:ready-for-proposal`, expressed as bindings to `RULE-001..007` in
`docs/product/mvp/08-backlog-shaping-rules.md`. It SHALL NOT duplicate the rules' content.

#### Scenario: a rubric change touches one file

- **WHEN** a backlog-shaping rule changes
- **THEN** the Definition of Ready reflects it without being edited, because it cites the rule

### Requirement: required fields

A ready issue SHALL carry: a capability-oriented title; the value in product terms; the main
actor (`ACT-*`); priority; dependencies; deterministic given/when/then acceptance criteria;
affected business rules (`BR-*`); affected use cases (`UC-*`); explicit out-of-scope; and the
change/spec ID that correlates issue → branch → PR → telemetry → retro.

#### Scenario: vague acceptance criteria are not ready

- **WHEN** an issue's acceptance criteria cannot be evaluated to true or false
- **THEN** the issue is not ready, and the missing determinism is named as the gap

#### Scenario: a Product item without a use case

- **WHEN** a Product-classified issue cites no `UC-*`
- **THEN** it is not ready

### Requirement: work blocked by an open decision cannot become ready

An issue that depends on an unresolved `OPN-*` SHALL NOT reach `ready-for-proposal`. A
decision-closure item SHALL be created and SHALL block it.

#### Scenario: the auth slice today

- **WHEN** an issue depends on OPN-002 (the Entra ID verification)
- **THEN** it stays blocked behind the decision-closure item rather than being proposed on an
  assumed answer

### Requirement: gaps are named, never bare-refused

When an issue fails the Definition of Ready, the response SHALL enumerate the specific unmet
fields, in a form that can be posted verbatim as an issue comment.

#### Scenario: a gap check

- **WHEN** an existing issue is checked and lacks acceptance criteria and a change ID
- **THEN** both are named explicitly, rather than the issue being reported as "not ready"

