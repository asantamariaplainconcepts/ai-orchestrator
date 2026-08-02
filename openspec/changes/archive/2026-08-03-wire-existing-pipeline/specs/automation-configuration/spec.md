# automation-configuration — delta for wire-existing-pipeline

## ADDED Requirements

### Requirement: setting a project up adopts the pipeline it already has

Setting up a project's Automations SHALL begin by finding the prompt files the repository already
carries, and SHALL wire Automations to those. Installing a starter SHALL happen only for a
pipeline step the repository has no file for.

A repository that already carries its own pipeline SHALL NOT receive a second copy of one. The
reason is the reason DEC-048 already gives for reading the grill's rubric from the project: a
product-wide version of a team's own document imposes one team's standards on every repository it
touches, and the copy is the weaker of the two.

**Discovery SHALL propose, never choose.** The conventional locations SHALL be searched — the
Connector's configured directory first, then `ai/prompts`, then `.claude/commands` and its
immediate subdirectories — and what was found SHALL be shown before anything is written. Where
more than one candidate holds files, all SHALL be offered and none SHALL be selected silently.
The prompts directory SHALL be saved only once a human has confirmed it.

Search SHALL go one subdirectory deep and no further: a form action that crawls a repository is a
different thing from one that looks where prompts conventionally live.

#### Scenario: a repository with its own pipeline is wired, not duplicated

- **WHEN** setting up a project whose repository already holds prompt files named for pipeline
  steps
- **THEN** Automations are wired to those files, and no starter is installed for those steps

#### Scenario: nothing is written before the human sees what was found

- **WHEN** discovery completes
- **THEN** the candidate directories and their files are reported, and no directory is saved and
  no Automation created until the choice is confirmed

#### Scenario: two candidates are both offered

- **WHEN** more than one conventional location holds prompt files
- **THEN** both are offered and neither is chosen automatically

#### Scenario: an empty repository gets the starters

- **WHEN** no conventional location holds a prompt file
- **THEN** every pipeline step is a gap, and the starter set is what fills it

### Requirement: a file is wired by its name, and an unrecognised one is reported

A prompt file whose name matches a pipeline step SHALL be wired to that step's trigger and
hand-off labels. A file that matches no step SHALL be reported as found and not wired, and SHALL
NOT produce an Automation.

A trigger SHALL NOT be invented from a filename. An Automation on a label nobody applies is the
configurable thing that silently never executes, which this capability already forbids elsewhere.

Where a step's trigger is already used by an enabled Automation, it SHALL be skipped and named —
the convergence rule this action already follows, so BR-003 can never fire from this path.

#### Scenario: a recognised name is wired

- **WHEN** the chosen directory holds a file named for a pipeline step
- **THEN** an Automation is created on that step's trigger, naming that file

#### Scenario: an unrecognised name is reported, not guessed

- **WHEN** the chosen directory holds a file matching no pipeline step
- **THEN** it is reported as found and not wired, and no Automation exists for it

#### Scenario: an existing trigger is skipped by name

- **WHEN** a step's trigger is already used by an enabled Automation
- **THEN** it is skipped and named in the report, and nothing collides

### Requirement: a step from an opt-in tier is adopted but never installed

A pipeline step belonging to a starter tier that declares a prerequisite SHALL be recognised and
wired when the repository already holds its file, and SHALL NOT be installed by this action when
it does not.

Reading a file a team wrote is not the same act as writing one they did not ask for. A tier that
declares what it assumes is opt-in by construction, and a button that installed it unprompted
would push a methodology into a repository whose team never chose it — the failure the tiering
was introduced to prevent.

#### Scenario: an opt-in step with a file is wired

- **WHEN** the chosen directory holds a file named for a step from a tier with a prerequisite
- **THEN** an Automation is created on that step's trigger, naming that file

#### Scenario: an opt-in step with no file is not installed

- **WHEN** a step from a tier with a prerequisite has no file in the chosen directory
- **THEN** no starter is written for it and it does not appear in the installed pull request

### Requirement: the setup reports what it did, in one place

The action SHALL report, in one summary: the directory chosen, the Automations created, the
Automations skipped and why, the files found but not wired, and the starters installed together
with the pull request carrying them.

Starters filling gaps SHALL be installed as **one** pull request rather than one per file: four
gaps are one decision, and four reviews of one decision is the cost this consolidation removes.

#### Scenario: one report, five facts

- **WHEN** the action completes
- **THEN** the summary names the directory, what was created, what was skipped and why, what was
  found but not wired, and what was installed

#### Scenario: gaps arrive as one pull request

- **WHEN** more than one step needs its starter installed
- **THEN** a single pull request carries them all
