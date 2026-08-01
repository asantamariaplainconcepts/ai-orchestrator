# automation-configuration

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

The output labels input SHALL be a picker that also accepts a freely typed value. It SHALL suggest
the trigger labels of the project's **other enabled** Automations, because wiring the next step of a
sequential workflow is what this field is most often for, and SHALL NOT suggest the Automation's own
trigger. Accepting free text SHALL remain possible, because a label may be a mark that triggers
nothing, or a trigger that does not exist yet.

A suggestion SHALL be a convenience only: every refusal SHALL also be enforced where the Automation
is saved, so a caller that does not use the portal is refused identically.

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

## REMOVED Requirements

### Requirement: a project can be given the framework's default Automations in one action

**Reason:** the defaults were "one of each action in the catalogue, wired together" (#162). With one
action left there is nothing to enumerate, and a default set that created several Automations
pointing at prompt files nobody has written would produce a project whose every Run fails on a
missing file.

**Migration:** removed with the catalogue, to return as prompt-and-grant bundles once grants exist —
which is the named follow-up. A project is configured by creating an Automation and writing the
prompt it names, and #150's missing-file failure already says the resolved path when the second half
has not happened yet.

### Requirement: the default trigger labels are ensured in the connected backlog

**Reason:** it existed to make the default set's labels selectable at the vendor the moment the set
was applied. With no default set there is nothing to ensure.

**Migration:** none. Ensuring a label at the vendor remains available where a label is written; what
goes is the bulk ensure that ran as part of applying defaults.

### Requirement: grill Automations carry their rubric path and ready label

**Reason:** the grill is not an action any more (#162). A rubric path is a field that only meant
something to it, and a readiness bar is a prompt — it is one in this repository already.

**Migration:** the field is **renamed**, not removed — #150 made that same column the way a
`RepositoryPrompt` names its prompt file, so it is `PromptPath` now and every value is kept. What
goes is the grill's half: the readiness bar moves into a prompt file, and the default ready label
goes with the action that defaulted it.
