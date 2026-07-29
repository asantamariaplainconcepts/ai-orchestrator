# automation-configuration

## REMOVED Requirements

### Requirement: grill Automations carry their rubric path and ready label

**Reason**: the grill action is retired (#162). This requirement is grill-specific — a rubric path and a
ready label held for one built-in — and there is no built-in left to hold them.

**Migration**: the path field survives as the repository prompt's file name, and the ready label survives
as the Automation's ordinary output label, which this change keeps (design D3).

### Requirement: a project can be given the framework's default Automations in one action

**Reason**: the defaults seeded catalogue actions that no longer exist (#162).

**Migration**: removed, to return as prompt-and-grant bundles once the grants model lands.

### Requirement: the default trigger labels are ensured in the connected backlog

**Reason**: the labels existed to make the seeded defaults selectable at the vendor; with the defaults
gone there is nothing to ensure.

**Migration**: removed with the defaults.

## MODIFIED Requirements

### Requirement: an Admin configures what a trigger label makes an Agent do

An Admin SHALL create an Automation on a Project consisting of a trigger label, an optional Story
state, the repository-prompt action with the name of a prompt file, a runtime, a `requiresApproval` flag,
and a phase timeout defaulting to 30 minutes (BR-005). The trigger label SHALL be required and
non-empty; the state SHALL be optional and compared as the vendor's own opaque string.

There SHALL be exactly one action. The locked catalogue of built-in actions is retired (#162, revising
DEC-026 and DEC-048): what an Automation does is decided by the prompt it names, in the project's own
repository, and the orchestrator SHALL NOT write to the vendor on the agent's behalf.

An Automation SHALL still carry an optional output label, and the orchestrator SHALL still write it on
success. That is the **workflow** — how steps connect — which DEC-053 separated from the catalogue and
which this change does not retire. It is distinct from the writes being removed: those completed the
agent's work, while the hand-off executes configuration the product itself declared and the prompt cannot
ask for.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, prompt file name, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** any action other than the repository prompt is submitted
- **THEN** it is refused as unknown — there is no longer a catalogue in which an action can be
  selectable but unimplemented
