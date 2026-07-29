# automation-configuration

## REMOVED Requirements

### Requirement: grill Automations carry their rubric path and ready label

**Reason**: the grill action is retired (#162), and the output label it wrote is retired with `HandOn`.

**Migration**: the path field survives as the repository prompt's file name; the ready label becomes
something the prompt writes itself if it wants a hand-off.

### Requirement: an Automation can hand work on by writing a label when it succeeds

**Reason**: hand-off was the orchestrator writing to the vendor on the agent's behalf, which is exactly
what #162 removes. A prompt that wants to hand work on writes the label itself.

**Migration**: the `OutputLabel` column is dropped. Chained configurations stop chaining; the prompts
that replace them carry the hand-off in their own text.

### Requirement: an Admin shapes the pipeline on a canvas

**Reason**: the canvas drew chains derived from output labels (#162 removes them), so it has nothing left
to draw. A canvas that draws a pipeline the product cannot execute misinforms.

**Migration**: the canvas is removed from the Automations tab; the catalogue remains.

### Requirement: an Admin places the human review by dragging it where the person belongs

**Reason**: the block's entire action was clearing a preceding step's output label (#137). With no output
label there is no chain to break.

**Migration**: removed with the canvas. A prompt that should wait for a person says so in its own text,
and `requiresApproval` still gates a Run's second phase.

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

An Automation SHALL NOT carry an output label. Handing work on SHALL be something a prompt does, not
something the row declares.

#### Scenario: creating an Automation

- **WHEN** an Admin submits a valid trigger label, prompt file name, runtime and approval flag
- **THEN** the Automation is stored against the Project and appears in its list

#### Scenario: an action with no implementation yet

- **WHEN** any action other than the repository prompt is submitted
- **THEN** it is refused as unknown — there is no longer a catalogue in which an action can be
  selectable but unimplemented
