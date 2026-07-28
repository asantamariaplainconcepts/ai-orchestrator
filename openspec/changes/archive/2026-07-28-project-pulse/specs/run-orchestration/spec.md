# run-orchestration

## ADDED Requirements

### Requirement: a project's pulse is readable from data the Runs already carry

The system SHALL expose a project pulse over a seven-day window, computed at read time from
persisted Runs and the existing Contracts surfaces — no new storage. It SHALL report runs
started, success rate over terminal runs only, total cost stating how many runs were excluded
for unknown usage, mean queue wait and mean duration from the recorded timestamps,
per-automation fire and failure counts including automations with zero runs, stories never run,
the project-scoped waiting summary, and the age of the oldest unanswered question. On the
project page, every reported figure SHALL link to the list it summarises.

#### Scenario: figures a Member can verify by hand

- **WHEN** a project has runs across states and ages and its pulse is requested
- **THEN** only the window's runs are counted, the success rate covers terminal runs only, and
  every mean derives from the recorded timestamps

#### Scenario: an unused automation appears

- **WHEN** an automation fired no runs inside the window
- **THEN** the pulse lists it as unused rather than omitting it

#### Scenario: unknown cost is stated, not guessed

- **WHEN** some window runs carry unknown usage
- **THEN** the cost sums the known runs and states how many were excluded

#### Scenario: an empty project has a pulse

- **WHEN** a project has no runs at all
- **THEN** the pulse returns zeros and empty collections, not an error
