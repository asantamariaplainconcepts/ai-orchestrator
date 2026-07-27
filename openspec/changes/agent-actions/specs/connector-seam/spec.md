# connector-seam

## ADDED Requirements

### Requirement: the Connector can comment on a Story and change its state

The seam SHALL expose adding a comment to a Story and setting a Story's state, in vendor-neutral
terms. A state the vendor does not accept SHALL be refused with a stated reason rather than
guessed at or silently ignored. Both SHALL reuse the existing error taxonomy.

#### Scenario: a comment reaches the vendor

- **WHEN** a comment is added through the seam
- **THEN** the vendor's Story carries it

#### Scenario: an unknown state is refused

- **WHEN** a transition names a state the vendor does not accept
- **THEN** the write is refused, naming the state, and nothing changes
