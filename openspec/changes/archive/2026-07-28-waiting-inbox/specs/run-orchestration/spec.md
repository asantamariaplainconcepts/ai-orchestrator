# run-orchestration

## ADDED Requirements

### Requirement: everything waiting on a human is visible in one place

The product SHALL list every Run waiting on a human — a plan awaiting approval, a question
awaiting an answer, a failure awaiting a decision — across all projects, newest wait first, each
entry naming the project, the story, the reason and since when, and linking to the Run. A
`Failed` Run SHALL leave the list once a newer Run exists for its Story, because it no longer
waits on anyone. An ambient count of waiting work SHALL be visible from every portal page,
driven by the same data as the list. An empty inbox SHALL present as the good state it is.

#### Scenario: three kinds of waiting, two projects, one list

- **WHEN** Runs are awaiting approval, awaiting input and failed across two projects
- **THEN** the inbox lists them all with project, story, reason and age, newest first

#### Scenario: an entry leads to its Run

- **WHEN** an entry is followed
- **THEN** the Member lands on that Run with the relevant action available

#### Scenario: the count is ambient

- **WHEN** any portal page is open with three Runs waiting
- **THEN** the shell shows 3, and resolving one updates it

#### Scenario: resolution removes

- **WHEN** a waiting Run resumes, is approved, or reaches a terminal state that waits on nobody
- **THEN** it leaves the list and the count

#### Scenario: a re-triggered failure waits on nobody

- **WHEN** a Failed Run's Story has a newer Run
- **THEN** the failure no longer appears

#### Scenario: empty is good

- **WHEN** nothing waits
- **THEN** the inbox says so, as a good state rather than an error
