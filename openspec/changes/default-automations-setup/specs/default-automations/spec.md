# default-automations — delta

## ADDED Requirements

### Requirement: the starter catalogue carries default Automation wiring as content

The portable tier's manifest entries MAY carry an `automation` block — trigger label,
`requiresApproval`, output labels — and the manifest-enumeration test SHALL refuse a wiring that
duplicates a trigger within the catalogue. The wiring is content beside the prompts it belongs
to: the product hardcodes no methodology.

#### Scenario: the wiring is enumerable and consistent

- **WHEN** the starter manifest is loaded
- **THEN** every `automation` block names a prompt in its own tier and no two blocks share a
  normalised trigger label

### Requirement: an Admin sets up the default Automations in one action

`POST /api/projects/{id}/automations/set-up-defaults` SHALL create an enabled Automation for
each wired portable-tier prompt whose trigger the project does not already have (compared
case-insensitively, the BR-003 comparison), and answer with three lists: created triggers,
skipped triggers, and the prompt paths the created Automations name that the repository does not
contain — with where each belongs. It MUST NOT write to the repository, and running it again
SHALL create nothing (idempotent). Admin-gated (BR-009).

#### Scenario: a fresh project gains the wired set

- **WHEN** the action runs on a project with no Automations
- **THEN** every wired starter Automation exists, enabled, with its catalogue wiring, and the
  response lists them as created

#### Scenario: existing triggers are skipped and named

- **WHEN** the project already has an Automation whose trigger matches a wired starter (any
  case)
- **THEN** that trigger is skipped and named in the response, everything else is created, and
  BR-003 never fires from this path

#### Scenario: missing prompt files are reported, not written

- **WHEN** a created Automation names a prompt file the repository does not contain
- **THEN** the response lists that path and where it belongs, and the repository is untouched

#### Scenario: a Member is refused

- **WHEN** a caller without the Automation-management permission invokes the action
- **THEN** the ordinary permission gate refuses it
