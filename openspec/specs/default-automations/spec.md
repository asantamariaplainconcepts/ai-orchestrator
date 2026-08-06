# default-automations Specification

## Purpose
A project's starter Automations created in one conflict-proof, idempotent action (#212), with the wiring carried by the starter catalogue as content (#190's discipline).
## Requirements
### Requirement: the starter catalogue carries default Automation wiring as content

The catalogue's manifest entries MAY carry an `automation` block — trigger label, `requiresApproval`,
output labels — and the manifest-enumeration test SHALL refuse a wiring that duplicates a trigger
within the catalogue. The wiring is content beside the prompts it belongs to: the product hardcodes no
methodology.

The duplicate-trigger refusal SHALL hold with no exception for any tier. A trigger SHALL have exactly
one prompt in the catalogue, so which file a step wires is never a function of which tier a caller
consented to.

#### Scenario: the wiring is enumerable and consistent

- **WHEN** the starter manifest is loaded
- **THEN** every `automation` block names a prompt in its own tier and no two blocks share a
  normalised trigger label

#### Scenario: a duplicated trigger is refused whatever tier it sits in

- **WHEN** two prompts in the catalogue claim the same normalised trigger, in the same tier or across
  tiers
- **THEN** the manifest-enumeration test fails

### Requirement: an Admin sets up the default Automations in one action

`POST /api/projects/{id}/automations/set-up-defaults` SHALL create an enabled Automation for
each wired catalogue prompt whose trigger the project does not already have (compared
case-insensitively, the BR-003 comparison), and answer with four lists: created triggers,
skipped triggers, triggers excluded by the caller's selection, and the prompt paths the created
Automations name that the repository does not contain — with where each belongs. It MUST NOT write
to the repository except through the install path below, and running it again SHALL create nothing
(idempotent). Admin-gated (BR-009).

The action SHALL accept an optional **selection** of triggers. An **absent** selection SHALL mean
every step, so a caller that sends no selection — including one that sends no body at all — behaves
exactly as before this capability existed. An **empty** selection SHALL mean no step: a lawful
no-op that creates nothing, writes nothing, and reports every step as excluded. Absent and empty
are different answers and SHALL NOT be conflated.

The action SHALL also accept an optional **tier consent** naming the tiers whose starters may be
installed. Unlike the selection, an **absent** consent SHALL mean **no tier**. The two defaults are
deliberately opposite and SHALL NOT be aligned for symmetry: a selection narrows a plan the caller has
already been shown, so including everything proposed is the safe default; a consent authorises writing
files into a repository, so authorising nothing is the safe default. A default that authorises would
make the safer field the more dangerous one.

A tier named in the consent that the catalogue does not contain SHALL match nothing — never an error.

A trigger named in the selection that the action would not otherwise have acted on SHALL match
nothing — never an error, and never work the action did not propose. Selection SHALL compare
triggers with the same case-insensitive identity BR-003 compares with (DEC-056).

Selection SHALL be applied **before** the already-exists and overlap checks, so a step the caller
excluded never reaches them: an excluded trigger SHALL appear in the excluded list and in no other.
Exclusion SHALL prevent creation only — it SHALL NOT delete, disable, or otherwise touch an
Automation the project already has.

Neither the selection nor the consent SHALL be persisted. A later invocation SHALL propose and act on
every step again, which is what keeps the convergence promise unconditional: after an invocation that
excludes nothing and consents to every tier, the wired set exists regardless of what any earlier caller
excluded or declined.

#### Scenario: a fresh project gains the wired set

- **WHEN** the action runs on a project with no Automations, consenting to every tier
- **THEN** every wired starter Automation exists, enabled, with its catalogue wiring, and the
  response lists them as created

#### Scenario: existing triggers are skipped and named

- **WHEN** the project already has an Automation whose trigger matches a wired starter (any
  case)
- **THEN** that trigger is skipped and named in the response, everything else is created, and
  BR-003 never fires from this path

#### Scenario: missing prompt files are reported, not written

- **WHEN** a created Automation names a prompt file the repository does not contain, and no tier was
  consented to
- **THEN** the response lists that path and where it belongs, and the repository is untouched

#### Scenario: a Member is refused

- **WHEN** a caller without the Automation-management permission invokes the action
- **THEN** the ordinary permission gate refuses it

#### Scenario: an absent selection means every step

- **WHEN** the action is invoked with no selection, or with no body at all
- **THEN** every step it would have acted on is acted on, and the excluded list is empty

#### Scenario: an absent consent means no tier

- **WHEN** the action is invoked with no tier consent, or with no body at all
- **THEN** no starter is installed, no branch is created, and no pull request is opened

#### Scenario: an empty selection means no step

- **WHEN** the action is invoked with an empty selection
- **THEN** no Automation is created, nothing is written to the repository, every step is reported as
  excluded, and the call succeeds

#### Scenario: only the selected steps are created

- **WHEN** the action is invoked with a selection naming some of the steps
- **THEN** an Automation exists for each selected step and for none of the others

#### Scenario: an excluded trigger is reported apart from a skipped one

- **WHEN** one step is excluded by the selection and another is skipped because the project already
  uses its trigger
- **THEN** the excluded one appears only in the excluded list, the skipped one only in the skipped
  list, and neither appears in both

#### Scenario: selection compares triggers case-insensitively

- **WHEN** a selection names a trigger in a different case from the catalogue's
- **THEN** it selects that step, the same identity BR-003 compares with

#### Scenario: a selected trigger the action would not act on matches nothing

- **WHEN** the selection names a trigger that is not among the steps this invocation would act on
- **THEN** the call succeeds, that name produces no Automation, and nothing is invented from it

#### Scenario: an unknown tier in the consent matches nothing

- **WHEN** the consent names a tier the catalogue does not contain
- **THEN** the call succeeds and nothing is installed on account of that name

### Requirement: starters are installed only for the gaps still selected

Where installing is asked for, the action SHALL write a starter only for a gap the selection kept **and
whose tier the caller consented to**. A gap the caller excluded SHALL NOT appear in the branch and
SHALL NOT appear in the pull request, and neither SHALL a gap belonging to a tier that was not
consented to.

Where the selection and the consent together leave **no** gap to fill, the action SHALL open no branch
and no pull request, and SHALL report installation as having written nothing — the same clean outcome as
a repository that already held every file. It SHALL NOT report a failure: an empty result the caller
chose is not a refusal, and surfacing one would tell an Admin their own decision went wrong.

#### Scenario: an excluded gap is not written

- **WHEN** a step with no file in the chosen directory is excluded, and other gaps remain selected
- **THEN** the pull request carries the selected gaps' files and not the excluded step's

#### Scenario: excluding every gap opens no pull request

- **WHEN** every step that would have had a starter installed is excluded
- **THEN** no branch is created, no pull request is opened, and installation reports no files and no
  failure

#### Scenario: an excluded step's file is left alone

- **WHEN** a step whose file the repository already holds is excluded
- **THEN** no Automation is created for it and the file is neither read into a wiring nor modified

#### Scenario: an unconsented tier's gap is not written

- **WHEN** a step has no file in the chosen directory and its tier was not consented to
- **THEN** its file is absent from the branch and the pull request, and no failure is reported

