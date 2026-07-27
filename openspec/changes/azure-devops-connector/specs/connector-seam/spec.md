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

## MODIFIED Requirements

### Requirement: the seam is normalised, not lowest-common-denominator

The abstraction SHALL express Stories in the product's own vocabulary — id, title, state, labels
— rather than mirroring any one vendor's schema. Vendor-specific mapping SHALL live in that
vendor's implementation. State values are the deliberate exception: they SHALL be carried
through verbatim, because with two real vendors in hand the vocabulary is owned by the
repository's or the project's own configuration, and any canonical set the product invented
would name states no board has (DEC-045).

#### Scenario: vendors disagree about vocabulary

- **WHEN** a vendor calls its concepts something other than Story, state or label
- **THEN** the mapping happens inside that vendor's implementation, and the rest of the system
  sees the product's vocabulary (DEC-005)

#### Scenario: state values stay the vendor's own

- **WHEN** a Story's state is read from either vendor
- **THEN** that vendor's own state value is carried through unaltered, and no product-wide state
  vocabulary is introduced
