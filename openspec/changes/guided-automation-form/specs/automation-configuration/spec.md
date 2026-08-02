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

**The form SHALL be organised as three questions, in the Automation's own execution order:** when it
fires, what it does, and what happens after. The grouping SHALL be visible, so that a reader who has
not been told the model can acquire it from the form rather than needing it beforehand.

**The form SHALL restate its own configuration in prose as it is filled**, in the vocabulary the
workflow surface uses. That restatement SHALL NOT be a second validation channel: an incomplete
configuration SHALL yield an incomplete sentence naming what is missing, and the field-level
refusals SHALL remain the only place a value is rejected.

**The approval control SHALL state its consequence** — that the Agent plans, stops, and waits for a
human, and that nothing executes until someone approves — and SHALL sit with the execution it gates
rather than with the form's submission.

**Ending the chain SHALL be an answer, not an absence.** The Admin SHALL choose between handing on
and stopping; choosing to stop SHALL store what an empty label set stores today, so nothing
downstream learns a new concept.

The output labels input SHALL be a picker that also accepts a freely typed value. It SHALL suggest
the trigger labels of the project's **other enabled** Automations, because wiring the next step of a
sequential workflow is what this field is most often for, and SHALL NOT suggest the Automation's own
trigger. Accepting free text SHALL remain possible, because a label may be a mark that triggers
nothing, or a trigger that does not exist yet.

Regrouping SHALL NOT change what is sent: for every configuration the form can express, the request
SHALL be identical to the one the ungrouped form produced.

#### Scenario: the form teaches its own model

- **WHEN** an Admin opens the New Automation form
- **THEN** its fields are grouped as when-it-fires, what-it-does and what-happens-after, and the
  grouping is visible without opening documentation

#### Scenario: a mistake is visible before saving

- **WHEN** an Admin fills the form
- **THEN** a sentence restates the configuration as prose and updates as the fields change

#### Scenario: an incomplete form is not an error

- **WHEN** required fields are still empty
- **THEN** the sentence names what is missing, and no rejection is raised outside the field-level
  refusals that already exist

#### Scenario: approval says what it does

- **WHEN** an Admin reads the approval control
- **THEN** it states that the Agent plans, stops and waits, and that nothing executes until someone
  approves

#### Scenario: stopping the chain is chosen, not left blank

- **WHEN** an Admin chooses to stop rather than hand on
- **THEN** the label control is not offered, and the Automation is stored with the empty label set
  that has always meant this

#### Scenario: regrouping changes no request

- **WHEN** any configuration expressible in the form is saved
- **THEN** the request body is identical to the one the four-column form produced
