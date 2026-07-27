# run-orchestration

## ADDED Requirements

### Requirement: a Run can wait for a human's answer on its Story and resume

A Run SHALL be able to enter a waiting state when its agent pass ends with questions for a human.
The questions SHALL be posted as a comment on the Story carrying a marker identifying the Run,
and no container SHALL remain running while the Run waits. Waiting SHALL be untimed, exactly as
approval waits are, and a waiting Run SHALL still block its Story and SHALL still be cancellable.
A comment on the Story newer than the questions and not carrying the marker SHALL resume the Run
through the ordinary dispatch path, and the resumed pass SHALL receive the re-read Story together
with the conversation so far rather than any stored transcript.

#### Scenario: the pass ends with questions

- **WHEN** an agent pass ends by asking
- **THEN** the Run is waiting, its questions are on the Story with the run marker, and no
  container is running

#### Scenario: the human answers

- **WHEN** a comment without the marker arrives after the questions
- **THEN** the Run returns to the queue and its next pass sees the Story and every comment
  exchanged

#### Scenario: the agent's own comment

- **WHEN** the only comment after the questions carries the run marker
- **THEN** nothing resumes — whoever's account posted it

#### Scenario: waiting is untimed but not immortal

- **WHEN** a waiting Run is cancelled after hours without an answer
- **THEN** it is terminal, its Story is free, and a later comment resumes nothing

#### Scenario: waiting blocks the Story

- **WHEN** a Story has a waiting Run and a trigger would start another
- **THEN** the second is refused exactly as it would be for an executing Run

#### Scenario: unrelated Stories

- **WHEN** a comment arrives on a Story with no waiting Run
- **THEN** nothing is dispatched
