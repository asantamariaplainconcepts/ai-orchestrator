## MODIFIED Requirements

### Requirement: two gates and two review stages

`ready-for-proposal` SHALL gate `/aio:propose`, and `ready-for-implementation` SHALL gate
`/aio:implement`. The two human review stages on the single PR SHALL each be marked by the **hold**
rather than by a state a reviewer must remember to set: the spec as a draft PR, held at
`status:ready-for-implementation`; then the code once marked ready, held at `status:code-review`. In
both, removing the hold is the reviewer's whole act, and the command that follows finds the issue
already in its gating state.

The **gating states are unconditional**; the **review stages are a property of the reviewed path**.
An unattended run (`/aio:ship`) SHALL still require `ready-for-proposal` to start and SHALL still
pass through `ready-for-implementation`, `in-progress` and `code-review` in order, leaving exactly one
`status:*` label at every moment — it applies no hold, so those states carry no review stage. A state
therefore says where the work is; whether a person is expected to look at it is said by the hold, and
by nothing else.

#### Scenario: gates are not skippable

- **WHEN** a command is invoked on an issue whose label is not its gating state
- **THEN** it refuses and names the command that advances the issue

#### Scenario: a review stage is a hold, not a state to set

- **WHEN** a reviewer finishes either review
- **THEN** they remove the hold and set no label, and the next command runs against the state its
  predecessor already applied

#### Scenario: an unattended run traverses the states without the stages

- **WHEN** `/aio:ship` carries an issue from `ready-for-proposal` to `done`
- **THEN** every intermediate state is set in order and the issue carries exactly one `status:*`
  label throughout, while no hold is applied and no review stage occurs

#### Scenario: a halted unattended run is indistinguishable from work awaiting a person

- **WHEN** an unattended run halts and applies the hold
- **THEN** the issue carries its current `status:*` label plus the hold, exactly as an issue parked
  at a review stage does, and the same act — a person removing the hold — releases it
