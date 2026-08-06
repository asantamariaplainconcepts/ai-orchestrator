# workflow-prerequisites Specification

## Purpose
TBD - created by archiving change spec-first-is-the-catalogue. Update Purpose after archive.
## Requirements
### Requirement: a starter tier declares the files its prompts need

A starter tier MAY declare **prerequisites**: files the tier's prompts read but that a repository
adopting the tier is not assumed to have. Each prerequisite SHALL name a repository-relative path and
SHALL carry the bytes to write there.

Prerequisites SHALL be catalogue content, declared in `src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/manifest.json`
beside the tier they belong to, and SHALL be embedded files rather than strings held in code — the
discipline starters already follow, so the bytes a test loads are the bytes a repository receives.

A prerequisite path SHALL be repository-relative and independent of the Connector's prompt directory.
These files have fixed homes (`docs/process/…`, `openspec/…`); resolving them against a prompt
directory would put a process document inside a prompt folder.

The product SHALL hardcode no prerequisite in a handler. A fork that edits the manifest SHALL change
what a consent writes without any code change.

#### Scenario: the prerequisites are enumerable from the catalogue

- **WHEN** the starter manifest is loaded
- **THEN** each tier's prerequisites are available with their paths and their content, and no
  prerequisite is named anywhere but the manifest

#### Scenario: every declared prerequisite has a file behind it

- **WHEN** the starter manifest is loaded
- **THEN** a prerequisite naming a file that is not embedded fails at load, rather than a tier being
  served with a hole in it

#### Scenario: every shipped prerequisite has a body

- **WHEN** the test suite enumerates the catalogue
- **THEN** every prerequisite loads and is asserted to have non-empty content, for the same reason a
  starter that fails to load is worse than none — it is offered as working

### Requirement: consenting to a tier writes its prerequisites in the same pull request

Where an Admin consents to a tier whose prompts are being installed, the action SHALL write that
tier's prerequisites into **the same branch and the same draft pull request** as the prompts. It SHALL
NOT open a second branch or a second pull request for them.

One press is one decision, and one decision SHALL cost one review. A workflow whose prompts and whose
documents arrive as two reviews can be merged half-way, which produces exactly the state the
prerequisite declaration exists to prevent: prompts that read documents the repository does not have.

#### Scenario: prompts and prerequisites arrive together

- **WHEN** a consented tier has prompt gaps and declares prerequisites the repository lacks
- **THEN** one branch and one draft pull request carry both the prompt files and the prerequisite
  files, and no second pull request is opened

#### Scenario: no consent, no prerequisite

- **WHEN** no tier is consented to
- **THEN** no prerequisite is written, no branch is created, and no pull request is opened

#### Scenario: a consented tier with no prompt gap still brings its prerequisites

- **WHEN** a consented tier's prompts all already exist in the chosen directory but its prerequisites
  do not
- **THEN** the prerequisites are written and the pull request carries them alone

### Requirement: prerequisites follow a tier that is actually acted on

Consent alone SHALL NOT cause a prerequisite to be written. A tier's prerequisites SHALL be written
only where the caller consented to that tier **and** at least one of that tier's steps survived the
caller's selection — whether that step is being wired to a file the repository already holds or having
a starter installed.

Where a caller consents to a tier and then excludes every one of its steps, the action SHALL write no
prerequisite for it, open no branch and no pull request, and report no failure. Consent answers *may
this workflow be installed*; the selection answers *what is being created*. An invocation that creates
nothing has not installed a workflow, and writing its documents anyway would be the press overriding
the checklist it had just shown.

This is what reconciles the two rules that would otherwise disagree: a consented tier whose prompts all
already exist still brings its documents, because those steps *are* being acted on; a consented tier
whose every step was excluded brings nothing, because none of them is.

#### Scenario: consenting and then excluding everything writes nothing

- **WHEN** a tier is consented to and every one of its steps is excluded by the selection
- **THEN** no prerequisite is written, no branch is created, no pull request is opened, and no failure
  is reported

#### Scenario: a partially selected tier still brings its prerequisites

- **WHEN** a tier is consented to and some but not all of its steps are selected
- **THEN** its prerequisites are written once, in the same pull request as the selected steps' files

### Requirement: an existing file always wins, prerequisites included

A prerequisite whose path already exists in the repository SHALL NOT be written, SHALL NOT be
modified, and SHALL NOT appear in the pull request. Its existing content SHALL be left exactly as it
is.

Presence SHALL be determined against the same default-branch content the action is about to branch
from, so the decision is made against what is really there rather than against a reading taken
earlier.

This is the rule `automation-configuration` already states for starters, applied to a file the product
did not previously write. It is also what keeps DEC-048's reasoning intact: a team that has its own
readiness document keeps it, so the product's copy never becomes "the weaker of the two" — the seed
lands only where there is no other.

#### Scenario: an existing process document is untouched

- **WHEN** the repository already holds a file at a declared prerequisite's path
- **THEN** that path is absent from the pull request and its content is unchanged

#### Scenario: an existing layout is skipped and the prompts still install

- **WHEN** every prerequisite path already exists but prompt gaps remain
- **THEN** no prerequisite is written, the prompt gaps are installed, and the pull request carries only
  the prompts

#### Scenario: everything already present is not a failure

- **WHEN** every prompt exists and every prerequisite path exists
- **THEN** nothing is written, no pull request is opened, and the action reports no failure

### Requirement: the report separates prerequisites from prompts

The action's report SHALL name the prerequisite files written as a fact distinct from the prompts
installed, and SHALL name the prerequisite paths skipped because they already existed.

An Admin who consented to a workflow's prompts SHALL be able to see, without opening the pull request,
that files outside the prompt directory were written to their repository. Folding both into one
"installed" list would let a count of prompts stand for a change to a repository's process documents.

#### Scenario: two kinds of write are two facts

- **WHEN** a consented press writes both prompt files and prerequisite files
- **THEN** the report lists them separately, so the writes outside the prompt directory are visible on
  their own

#### Scenario: a skipped prerequisite is reported as already present

- **WHEN** a prerequisite is skipped because its path exists
- **THEN** the report names it as already present, rather than omitting it or counting it as written

