## ADDED Requirements

### Requirement: a tier that writes beyond prompts is consented to by name

Where a starter tier declares a prerequisite, the setup card SHALL present that tier with a consent
control that is **off** by default, and the action SHALL install that tier's prompts only where the
caller named it.

The control SHALL state its consequence before it is given: the tier's prerequisite text, and the paths
a press with it on would write — both the prompts and the files outside the prompt directory. The
statement SHALL be computed from the discovery already performed, and SHALL NOT cost a vendor read per
tier.

The control SHALL remain reachable when the plan is empty. A repository with no pipeline and no consent
has no rows at all, and a consent that lived inside the row list would be unreachable in exactly the
case it exists for.

Consent SHALL be per-invocation and SHALL NOT be persisted. Reopening the card SHALL show the control
off, whatever an earlier press consented to.

Consent SHALL compare tier identifiers exactly as declared in the catalogue.

#### Scenario: the control is off until it is turned on

- **WHEN** the setup card shows a tier that declares a prerequisite
- **THEN** its consent control is off, and none of that tier's steps is selected

#### Scenario: the consent says what it will write

- **WHEN** the consent control is shown
- **THEN** the tier's prerequisite text and the paths a press would write are shown with it, computed
  without an additional vendor read

#### Scenario: the consent is reachable with an empty plan

- **WHEN** the chosen directory holds no file for any step, so the plan has no rows
- **THEN** the consent control is still shown and can still be turned on

#### Scenario: consent is not remembered

- **WHEN** a press consented to a tier and the card is opened again
- **THEN** the control is off

#### Scenario: an unconsented tier installs nothing

- **WHEN** the action runs with no tier named
- **THEN** no starter is installed, no branch is created, and no pull request is opened

## MODIFIED Requirements

### Requirement: starters are labelled by what they require

The set SHALL be presented in tiers distinguished by prerequisite, and the prerequisites SHALL be
stated on the surface rather than discovered when an agent cannot find a file.

A tier that names no document outside the project's own repository SHALL declare no prerequisite, and
each of its starters SHALL state the capability it still needs where it needs one — push access, a
vendor command line, a test command.

A tier MAY contain starters belonging to a particular way of working, and SHALL state what that way of
working requires. A starter that reads documents a fresh repository does not have SHALL NOT be
presented as though it assumed only the repository.

The tiering SHALL describe the catalogue as it is, and SHALL NOT require any particular tier to exist.
A catalogue of one tier is lawful: what the requirement fixes is that a tier's assumptions are
declared, not how many tiers ship.

#### Scenario: a prerequisite is visible before it is needed

- **WHEN** a starter requires a tool or document beyond the repository
- **THEN** that requirement is shown with the starter, not learned from a failed Run

#### Scenario: the tiers are distinguishable

- **WHEN** the set is offered
- **THEN** a starter that assumes only the repository is distinguishable from one that assumes a way of
  working

#### Scenario: one tier is a lawful catalogue

- **WHEN** the catalogue ships a single tier that declares a prerequisite
- **THEN** the set is offered with that tier's requirement stated, and nothing is presented as
  assuming only the repository

### Requirement: a step from an opt-in tier is adopted, and installed only on consent

A pipeline step belonging to a starter tier that declares a prerequisite SHALL be recognised and wired
when the repository already holds its file.

Where the repository does not hold its file, the step SHALL be installed **only where the caller has
consented to its tier by name**, and SHALL NOT be installed otherwise.

Reading a file a team wrote is not the same act as writing one they did not ask for. A tier that
declares what it assumes is opt-in by construction, and a button that installed it *unprompted* would
push a methodology into a repository whose team never chose it — the failure the tiering was introduced
to prevent. A consent that is off by default, names the tier, and states the paths it will write is not
unprompted; it is the prompt.

#### Scenario: an opt-in step with a file is wired

- **WHEN** the chosen directory holds a file named for a step from a tier that declares a prerequisite
- **THEN** an Automation is created on that step's trigger, naming that file

#### Scenario: an opt-in step with no file and no consent is not installed

- **WHEN** a step from a tier that declares a prerequisite has no file in the chosen directory and its
  tier was not consented to
- **THEN** no starter is written for it and it does not appear in any pull request

#### Scenario: an opt-in step with no file is installed once its tier is consented to

- **WHEN** a step from a tier that declares a prerequisite has no file in the chosen directory and its
  tier was consented to
- **THEN** its starter is written, an Automation is created on its trigger naming the installed file,
  and both arrive in one draft pull request

### Requirement: setting a project up adopts the pipeline it already has

Setting up a project's Automations SHALL begin by finding the prompt files the repository already
carries, and SHALL wire Automations to those. Installing a starter SHALL happen only for a
pipeline step the repository has no file for.

A repository that already carries its own pipeline SHALL NOT receive a second copy of one. The
reason is the reason DEC-048 already gives for reading the grill's rubric from the project: a
product-wide version of a team's own document imposes one team's standards on every repository it
touches, and the copy is the weaker of the two.

That comparison presumes there are two. Where a repository has **no** file at a path a consented tier
would write, there is no team's own version to be weaker than, and the product MAY seed one — revised by
DEC-064 and recorded in `docs/adr/0012-a-seeded-document-is-the-projects-own.md`. The rule above is
unchanged in the case it was written about: an existing file still always wins.

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

#### Scenario: the seeding revision is reachable from the rule it narrows

- **WHEN** a reader finds the adoption rationale citing DEC-048
- **THEN** the decision that narrowed it, and the ADR recording why, are named there

### Requirement: the setup card says what it will create before it is pressed

Where a pipeline has been discovered, the portal SHALL show what pressing the build control would
create, **before** it is pressed. The plan SHALL list one row per step, naming the trigger, the
prompt file that step would wire, whether that file already exists in the repository or a starter
would be installed for it, and whether the step waits for a person.

The plan SHALL be computed from the discovery the card has already performed. It SHALL NOT require a
second endpoint, and SHALL NOT cost an additional vendor read per row.

A step that would be wired but for which no starter can be installed SHALL be distinguishable from
one that would have a starter written, because those differ in whether anything is written to the
repository.

A step that neither has a file in the chosen directory nor can have a starter installed SHALL NOT
appear in the plan at all: nothing would happen for it either way, and a row offering a choice that
changes nothing is noise in a list whose whole purpose is to say what the press will do. A step whose
tier has not been consented to is such a step, so consenting to a tier SHALL bring its installable
steps into the plan, and withdrawing that consent SHALL remove them.

**No separate consent SHALL be required for installing the starters the plan names.** The rows state
which files would be written; a control asking whether to write them restates the preview, and a
confirmation of a confirmation trains a reader past both.

That rule governs the prompts a row names. It SHALL NOT be read as forbidding the tier consent, which
authorises a **different** act: writing files outside the prompt directory, at paths no row names, on
the terms of a methodology the plan does not describe. The test is whether the control asks a question
the plan has already answered — the tier consent asks one the plan cannot.

The statement that starters arrive as a draft pull request SHALL sit with the control that creates
them, because that is where the decision is taken.

A plan longer than a few rows SHALL collapse, and SHALL be expandable — a plan that fills the screen
stops being read, which defeats showing it.

#### Scenario: the plan precedes the press

- **WHEN** a pipeline has been discovered
- **THEN** one row per step is shown, naming the trigger, the file it wires, whether that file exists
  and whether the step waits for a person

#### Scenario: reading the plan changes nothing

- **WHEN** the plan is computed
- **THEN** no Automation is created and nothing is written to the repository

#### Scenario: the preview replaces the consent for the files it names

- **WHEN** the plan is visible
- **THEN** no separate control asks whether to install the starter files its rows name

#### Scenario: a step nothing would happen for is not offered

- **WHEN** a step has no file in the chosen directory and no starter can be installed for it
- **THEN** it does not appear as a row in the plan

#### Scenario: consenting to a tier grows the plan

- **WHEN** a tier that declares a prerequisite is consented to
- **THEN** its installable steps appear as rows, each stating that a starter would be installed, and
  withdrawing the consent removes them again
