# definition-of-ready Specification (delta)

## MODIFIED Requirements

### Requirement: the Definition of Ready cites the backlog rules rather than restating them

`docs/process/definition-of-ready.md` SHALL define the bar an issue must meet to reach
`status:ready-for-proposal`, expressed as bindings to `RULE-001..007` in
`docs/product/v1/08-backlog-shaping-rules.md`. It SHALL NOT duplicate the rules' content.

#### Scenario: a rubric change touches one file

- **WHEN** a backlog-shaping rule changes
- **THEN** the Definition of Ready reflects it without being edited, because it cites the rule
