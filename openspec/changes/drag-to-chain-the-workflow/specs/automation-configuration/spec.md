## ADDED Requirements

### Requirement: the workflow's shape is edited from the picture that draws it

An Admin SHALL be able to place an Automation into the drawn chain, and take one out of it, by
direct manipulation of the picture. Every such gesture SHALL be an ordinary Automation update that
changes **output labels only** — the graph stays derived, so the picture cannot come to claim a
chain that would not fire.

Before a placement happens, the position it would take SHALL state the wiring it performs, naming
each hand-off it would create. A placement onto the end of a chain states the one hand-off it makes.

A placement that cannot be performed SHALL refuse **at the position it was offered**, naming the
rule that stops it rather than the symptom: a trigger shared with another enabled Automation
(BR-003), a loop, a step handed to itself, or an edge that already exists. The refusal SHALL be
shown while the gesture is still in progress, never reported after it.

Refusing here SHALL NOT be the enforcement. The update the gesture performs is checked where every
other Automation update is checked, so a rule lives in one place and is explained in another.

Every capability reachable by direct manipulation SHALL also be reachable without it.

After any such change, the drawn chain, the catalogue's account of which Automations are wired in,
and the count of steps SHALL agree with one another.

#### Scenario: the position says what it would wire

- **WHEN** an Admin holds a standalone Automation over a position between two chained steps
- **THEN** that position names both hand-offs the placement would create, before it happens

#### Scenario: placing rewrites labels and nothing else

- **WHEN** the Automation is placed between those two steps
- **THEN** the preceding step hands to it, it hands to what followed, no other field of any
  Automation changes, and the drawn chain shows the new order

#### Scenario: placing at the end makes one hand-off

- **WHEN** an Automation is placed at the end of a chain
- **THEN** the last step hands to it, and nothing else changes

#### Scenario: a refused placement names its rule

- **WHEN** a placement would share a trigger with another enabled Automation, close a loop, hand a
  step to itself, or repeat an edge that exists
- **THEN** the position refuses while the gesture is in progress, names that rule, and performs
  nothing

#### Scenario: taking a step out returns it to the catalogue

- **WHEN** a step drawn in the chain is placed back on the catalogue
- **THEN** whatever handed to it stops doing so, and nothing is invented in its place

#### Scenario: the surfaces agree

- **WHEN** any placement completes
- **THEN** the chain, the catalogue's in-workflow marking and the step count describe the same
  workflow

### Requirement: the workflow shows the board it produces

Where a workflow has at least one chain, the Admin SHALL be shown what that workflow makes of the
Backlog: one column per step, in the order work moves through them, preceded by where Stories
start.

That view SHALL be derived from the same chains the canvas draws — never a second description of
the workflow, which could disagree with the first. It SHALL mark the columns that wait for a
person's approval, and SHALL show where the flow stops and a person carries the work onward.

It SHALL be read-only. Wiring happens on the chain; this reacts to it.

A column that a placement has just added SHALL be distinguished, so the consequence of the gesture
is visible where it landed.

#### Scenario: the columns are the workflow's steps

- **WHEN** a workflow of three chained steps is drawn
- **THEN** the preview shows where Stories start followed by those three columns, in that order

#### Scenario: a gate and a stop are both visible

- **WHEN** one step waits for approval and the chain ends without handing on
- **THEN** that column is marked as gated, and the end of the flow shows the person who carries it

#### Scenario: a new column is shown where it landed

- **WHEN** an Automation is placed into the chain
- **THEN** its column appears in the preview and is distinguished from the others
