# agent-execution

## ADDED Requirements

### Requirement: the Agent's instruction carries the Story's requirement

The prompt built for a Run SHALL include the Story's mirrored description alongside its title,
state and labels — an Agent asked to implement a Story SHALL NOT be working from a headline
alone. The body SHALL be bounded at the prompt (not at rest) so an unusually long description
cannot turn into an unbounded cost or a timeout surprise.

#### Scenario: the requirement reaches the Agent

- **WHEN** a Run executes for a Story with a description
- **THEN** the instruction handed to the runtime contains that description

#### Scenario: a very long description is bounded

- **WHEN** the description exceeds the prompt's bound
- **THEN** the instruction carries a truncated body and says it was truncated
