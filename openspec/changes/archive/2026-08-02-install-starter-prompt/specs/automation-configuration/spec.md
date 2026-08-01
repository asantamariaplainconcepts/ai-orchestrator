# automation-configuration — delta for install-starter-prompt

## MODIFIED Requirements

### Requirement: a new project is offered a starter set of prompts

The portal SHALL offer a versioned set of starter prompts, so that a project with an empty prompts
directory has something to take rather than only a refusal naming a path that does not exist.

Every starter SHALL be presented with its purpose in one sentence, the filename it is meant to be
saved as, and its content. The filename and the directory it belongs in SHALL be the ones the Run
path already resolves, so taking a starter and creating an Automation that names it is two steps and
no translation.

Every starter SHALL carry frontmatter of the kind the Run path already strips, so a file taken from
the set behaves identically whether this product runs it or a local agent runner does.

**Offering SHALL write nothing.** The offer presents content; no agent pass SHALL be spent
producing content the product already holds. Writing happens only through the explicit install
action (#214), which is its own requirement below — it spends no agent pass either, and it never
touches the default branch.

#### Scenario: an empty project has something to take

- **WHEN** an Admin looks at a project with no prompts
- **THEN** the starter set is offered, each entry with its purpose, its filename and its content

#### Scenario: a starter is a prompt an Automation can name

- **WHEN** an Admin saves a starter under the filename offered, in the project's prompts directory
- **THEN** an Automation naming that file resolves it, with no renaming or path translation

#### Scenario: a starter behaves the same outside this product

- **WHEN** a starter's frontmatter is stripped as the Run path strips it
- **THEN** a non-empty body remains

#### Scenario: offering writes nothing

- **WHEN** the starter set is offered
- **THEN** nothing is written to the project's repository and no agent pass is run

## ADDED Requirements

### Requirement: a starter can be installed as a draft pull request

Where a project has a Connector, each starter SHALL offer an **install** action that writes the
starter's bytes at `<prompts directory>/<filename>` on a starter-scoped branch and opens a
**draft pull request** — through the same workspace publish pipeline Runs use (clone with the PAT
resolved by name at use, BR-010; commit; push; PR), with no agent pass spent. The default branch
SHALL never be written; a human merges.

The opened PR's URL SHALL be shown where the install was asked for, stating that review is the
human's next step. Each pipeline failure SHALL name its stage — clone, push, or PR — in the same
voice as implement's refusals.

A starter already present at its target path on the default branch SHALL refuse install naming the
path — an existing file always wins, now enforced at the moment it matters rather than holding by
construction. Where the project has no Connector, install SHALL NOT be offered; the offer itself
remains usable as today.

#### Scenario: one click, one reviewable PR

- **WHEN** an Admin installs a starter on a project with a Connector
- **THEN** a branch carries the file at the prompts directory under the starter's filename, a
  draft pull request exists, and its URL is shown on the starter

#### Scenario: the default branch is never written

- **WHEN** any install completes
- **THEN** the default branch is unchanged until a human merges the pull request

#### Scenario: already present refuses by name

- **WHEN** the starter's target path already exists on the default branch
- **THEN** install is refused naming that path, and no branch or PR is created

#### Scenario: a stage failure says which stage

- **WHEN** the clone, the push, or the PR creation fails
- **THEN** the refusal names that stage and repeats the vendor's reason

#### Scenario: no Connector, no install

- **WHEN** a project has no Connector
- **THEN** the starter is offered without the install action
