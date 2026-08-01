# automation-configuration — delta for prompt-picker

## MODIFIED Requirements

### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL configure, per Project, which trigger label starts a Run, on which runtime, with which
timeout and whether the plan requires approval. Every field SHALL be bounded, and the refusal for an
unbounded or unparseable value SHALL name the field.

**The action is one and only one: run the repository's prompt (#162).** Any other action SHALL be
refused with the ordinary unknown-action refusal. What an Automation *does* is decided by the prompt
file in the project's repository, read live at Run time, and not by anything chosen here.

**Naming that prompt SHALL be required.** With one action, an Automation that names no prompt can
never run, and a configurable thing that silently never executes is the trap this spec already
forbids. The refusal SHALL land where the Admin is looking — at save — rather than at the first Run,
in front of somebody who did not configure it.

**The prompt-name input SHALL be a picker that also accepts a freely typed value (#215).** It SHALL
offer the `.md` files currently in the project's prompts directory, read live from the repository's
default branch through the Connector at the moment the field is used — never cached and never
mirrored. Names SHALL be offered relative to the prompts directory, one directory level deep,
because that is what the Automation stores. Free text SHALL remain accepted, because a prompt may
be arriving in a pending pull request. When the directory does not exist, the listing fails, or the
project has no Connector, the field SHALL degrade to the plain text input with the reason readable —
configuration SHALL never be blocked by discovery.

The output labels input SHALL be a picker that also accepts a freely typed value. It SHALL suggest
the trigger labels of the project's **other enabled** Automations, because wiring the next step of a
sequential workflow is what this field is most often for, and SHALL NOT suggest the Automation's own
trigger. Accepting free text SHALL remain possible, because a label may be a mark that triggers
nothing, or a trigger that does not exist yet.

A suggestion SHALL be a convenience only: every refusal SHALL also be enforced where the Automation
is saved, so a caller that does not use the portal is refused identically — and the prompt picker
SHALL follow the same rule: a name not among the suggestions saves exactly as a typed one, and the
missing-prompt refusal stays where it always was, at Run time (#150).

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, action, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** an action is offered that no Agent can execute
- **THEN** it is not offered at all — the catalogue is one action, and a selectable action that
  cannot run has no way to exist

#### Scenario: the form offers what is wirable

- **WHEN** an Admin opens the output labels input on a project with other enabled Automations
- **THEN** their trigger labels are offered, and this Automation's own trigger is not

#### Scenario: a disabled Automation is not offered

- **WHEN** another Automation in the project is disabled
- **THEN** its trigger is not among the suggestions

#### Scenario: a label nobody listens to is still allowed

- **WHEN** an Admin types a label that matches no trigger
- **THEN** it is accepted and applied on success like any other

#### Scenario: an Automation must name its prompt

- **WHEN** an Automation is saved naming no prompt
- **THEN** it is refused, because with one action it could never run

#### Scenario: the vocabulary is one word

- **WHEN** an Automation is saved naming any of the retired actions
- **THEN** it is refused, because they are not actions any more

#### Scenario: the prompt field offers what the repository holds

- **WHEN** an Admin opens the prompt-name input on a project whose prompts directory holds markdown
  files on the default branch
- **THEN** those file names are offered, relative to the prompts directory

#### Scenario: a prompt not yet merged can still be named

- **WHEN** an Admin types a prompt name that the listing did not offer
- **THEN** it saves exactly as a listed one would, and a Run finding no such file fails with the
  resolved path, as #150 specified

#### Scenario: discovery failure does not block configuration

- **WHEN** the prompts directory does not exist, the listing fails, or the project has no Connector
- **THEN** the field renders as the plain text input with the reason readable, and the Automation
  can still be saved
