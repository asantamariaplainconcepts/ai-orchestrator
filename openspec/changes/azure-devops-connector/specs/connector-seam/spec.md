# connector-seam

## ADDED Requirements

### Requirement: a second vendor implements the seam without changing it

The Connector seam SHALL support Azure DevOps alongside GitHub, with every method implemented
and no vendor SDK type escaping the implementation. Trigger vocabulary SHALL map to the
vendor's own (tags on Azure DevOps), so matching behaves identically whichever vendor an event
came from (BR-015). Where a concept is **process-dependent** — the state vocabulary, the
estimate field — the connector SHALL NOT assume one: it SHALL attempt what was asked and
surface the vendor's refusal, naming what it tried. A Connector MAY name a code repository
separate from its backlog location, used only for cloning; vendors whose code and backlog
coincide SHALL leave it empty.

#### Scenario: the same event whichever vendor produced it

- **WHEN** a story changes on either vendor and is reconciled
- **THEN** matching receives the same `StoryChanged`, with no vendor-specific handling

#### Scenario: a process-dependent field is refused, not guessed

- **WHEN** a state or estimate is written to a project whose process does not accept it
- **THEN** the failure names what was attempted, and nothing is silently skipped

#### Scenario: the guardrails hold with two vendors

- **WHEN** the guardrail suite runs
- **THEN** no vendor SDK appears outside the Connectors folder and the seam's types remain
  vendor-neutral
