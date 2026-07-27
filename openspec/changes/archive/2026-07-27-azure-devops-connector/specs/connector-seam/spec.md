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

## REMOVED Requirements

### Requirement: the seam is normalised, not lowest-common-denominator

**Reason:** its state-value scenario was written while OPN-003 was open and says the mapping is
still to be chosen. The decision is now made (DEC-045) and it is the opposite of provisional, so
the requirement is restated below rather than left describing a pending choice.

**Migration:** none — the behaviour is unchanged. Only the requirement's stated reason changes,
from "not decided yet" to "decided, and deliberately vendor-owned".

## ADDED Requirements

### Requirement: the seam is normalised, and state values are the deliberate exception

The abstraction SHALL express Stories in the product's own vocabulary — id, title, state, labels
— rather than mirroring any one vendor's schema. Vendor-specific mapping SHALL live in that
vendor's implementation.

State values SHALL be carried through verbatim from whichever vendor produced them, and the
product SHALL NOT define a canonical state vocabulary. With two real vendors implemented this is
settled rather than deferred (DEC-045): a GitHub repository and an Azure DevOps process template
each own their own state names, and any set the product invented would name states that no board
has and force every write to guess a translation.

#### Scenario: vendors disagree about vocabulary

- **WHEN** a vendor calls its concepts something other than Story, state or label
- **THEN** the mapping happens inside that vendor's implementation, and the rest of the system
  sees the product's vocabulary (DEC-005)

#### Scenario: state values stay the vendor's own

- **WHEN** a Story's state is read from either vendor
- **THEN** that vendor's own state value is carried through unaltered

#### Scenario: a state the vendor will not accept

- **WHEN** a state is written that the vendor's process does not allow
- **THEN** the vendor's refusal is surfaced naming what was attempted, rather than being mapped
  onto some nearest product state
