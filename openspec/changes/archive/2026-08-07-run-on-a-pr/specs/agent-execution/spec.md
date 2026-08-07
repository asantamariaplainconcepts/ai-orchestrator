## ADDED Requirements

### Requirement: a change-targeted Run is prepared on the change's own head branch

For a Run that targets an open change, the Run ceremony SHALL prepare the workspace on the
change's **existing head branch** — the named-branch checkout the install path already performs —
instead of cutting a fresh `run/<id>` branch. Pushing the work SHALL remain what it already is for
every Run since the publish step was retired (DEC-062): the Agent's own act, performed with the
credential the instruction carries. No workspace publish step SHALL be reintroduced for this —
one retired ceremony must not come back wearing a new name.

The instruction the Agent receives SHALL carry the change's context — its number, title and head
branch — in place of the Story framing a story Run gets, and SHALL carry the Member's ad-hoc text
as the prompt body. An Agent that changes nothing and pushes nothing SHALL leave a succeeded Run
whose record says the change was left untouched — no changes is an answer to an instruction, not
a failure.

#### Scenario: the workspace is the change's branch

- **WHEN** a change-targeted Run is executed
- **THEN** the workspace is prepared on the change's head branch by name, and no `run/<id>`
  branch is created

#### Scenario: the checkout failure names its stage

- **WHEN** the change's head branch no longer exists at prepare time
- **THEN** the Run fails with the ceremony's stage-named checkout reason

#### Scenario: the framing is the change's, not a Story's

- **WHEN** the Agent's instruction is assembled for a change-targeted Run
- **THEN** it carries the change's number, title and head branch and the ad-hoc text, and no
  Story lookup is performed
