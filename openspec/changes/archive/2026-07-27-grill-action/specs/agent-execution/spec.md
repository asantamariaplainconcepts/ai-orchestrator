# agent-execution

## ADDED Requirements

### Requirement: the grill action interrogates a Story to its project's readiness bar

A `GrillToReady` Run SHALL evaluate the Story's current body and its conversation so far against
a readiness document read live from the connected repository. When criteria are unmet, the pass
SHALL end by asking — the specific unmet criteria, posted through the conversational wait — and
questions SHALL NOT repeat what the conversation has already settled. When the bar is met, the
Run SHALL apply the configured ready label through the ordinary label write and post a verdict
comment naming the criteria, then succeed. A missing readiness document SHALL fail the Run naming
the configured path, before any comment or label is written.

#### Scenario: gaps become questions

- **WHEN** a grilled Story is missing criteria the rubric demands
- **THEN** the Run's comment names them specifically and the Run waits for input

#### Scenario: answers close the gaps

- **WHEN** the resumed pass finds the conversation satisfies the rubric
- **THEN** the ready label is applied, the verdict comment names the criteria, and the Run
  succeeds

#### Scenario: already ready

- **WHEN** a Story that meets the bar is grilled
- **THEN** the first pass marks it ready with no questions asked

#### Scenario: no rubric, no grill

- **WHEN** the configured rubric path does not exist in the repository
- **THEN** the Run fails naming that path, and the Story is untouched

#### Scenario: the chain is ordinary matching

- **WHEN** the ready label is another enabled Automation's trigger
- **THEN** that Automation triggers through reconciliation and matching, with no dedicated
  chaining code
