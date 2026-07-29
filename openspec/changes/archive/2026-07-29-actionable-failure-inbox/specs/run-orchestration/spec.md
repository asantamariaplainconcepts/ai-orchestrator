# run-orchestration

## MODIFIED Requirements

### Requirement: everything waiting on a human is visible in one place

The product SHALL list every Run waiting on a human — a plan awaiting approval, a question
awaiting an answer, a failure awaiting a decision — across all projects, newest wait first, each
entry naming the project, the story, the reason and since when, and linking to the Run. A
`Failed` Run SHALL leave the list once a newer Run exists for its Story, because it no longer
waits on anyone. An ambient count of waiting work SHALL be visible from every portal page,
driven by the same data as the list. An empty inbox SHALL present as the good state it is.

A `Failed` Run SHALL also leave the list when a human has dismissed it, which is the decision that
no re-run is intended. Dismissal SHALL be recorded on the Run with the time it happened, because
nothing in the data distinguishes "nobody has decided yet" from "somebody decided not to act" — the
newer-Run condition stays derived, and this one is stored because a decision cannot be derived. A
dismissed Run SHALL remain `Failed`: dismissal records that somebody looked, and SHALL NOT change
what happened or cause anything to run.

Every count of failures awaiting a decision SHALL use the same condition as the list, so a count and
a list can never disagree.

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

#### Scenario: a dismissed failure waits on nobody

- **WHEN** a Member dismisses a Failed Run
- **THEN** it leaves the list and every count of failures awaiting a decision, and the Run is still
  `Failed`

#### Scenario: a dismissal is readable afterwards

- **WHEN** a dismissed Run is viewed
- **THEN** the dismissal and when it happened are visible

#### Scenario: only a failure can be dismissed

- **WHEN** dismissal is attempted on a Run that is not `Failed`
- **THEN** it is refused and nothing changes

#### Scenario: empty is good

- **WHEN** nothing waits
- **THEN** the inbox says so, as a good state rather than an error

## ADDED Requirements

### Requirement: a Member re-runs a failure from where the failure is

A Member SHALL be able to re-run a `Failed` Run from that Run, without navigating elsewhere to find
the Story or choose an Automation. The new Run SHALL be created through the same path manual dispatch
already uses, with the failed Run's own Automation, so one active Run per Story, the project cap and
the approval gate all apply unchanged (BR-001, BR-002, BR-013).

Re-running SHALL NOT alter the failed Run, which remains the record of what happened. It leaves the
inbox because a newer Run exists, which is the condition already in force.

A re-run refused because the Story already holds an active Run SHALL be reported in the same terms
manual dispatch uses for the same refusal.

#### Scenario: re-running from the failure

- **WHEN** a Member re-runs a Failed Run
- **THEN** a new Run exists for the same Story with the same Automation, the failed Run is unchanged,
  and the failure leaves the inbox

#### Scenario: the Story is already busy

- **WHEN** a Member re-runs a Failed Run whose Story holds an active Run
- **THEN** the attempt is refused naming one active Run per Story, in the same terms manual dispatch
  uses

#### Scenario: the gate still applies

- **WHEN** the failed Run's Automation requires approval
- **THEN** the new Run waits for a plan to be approved exactly as manual dispatch would produce
