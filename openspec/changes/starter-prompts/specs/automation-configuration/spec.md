# automation-configuration

## ADDED Requirements

### Requirement: a new project is offered a starter set of prompts

The portal SHALL offer a versioned set of starter prompts, so that a project with an empty prompts
directory has something to take rather than only a refusal naming a path that does not exist.

Every starter SHALL be presented with its purpose in one sentence, the filename it is meant to be
saved as, and its content. The filename and the directory it belongs in SHALL be the ones the Run
path already resolves, so taking a starter and creating an Automation that names it is two steps and
no translation.

Every starter SHALL carry frontmatter of the kind the Run path already strips, so a file taken from
the set behaves identically whether this product runs it or a local agent runner does.

**The product SHALL NOT write any starter to any repository.** It offers the content; the Admin puts
the file in their repository. No agent pass SHALL be spent producing content the product already
holds, and no repository write capability SHALL be added for this purpose.

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

### Requirement: starters are labelled by what they require

The set SHALL be presented in tiers distinguished by prerequisite, and the prerequisites SHALL be
stated on the surface rather than discovered when an agent cannot find a file.

A **portable** tier SHALL contain starters that name no document outside the project's own
repository, and each SHALL state the capability it still needs where it needs one — push access, a
vendor command line, a test command.

A **workflow** tier MAY contain starters belonging to a particular way of working, and SHALL state
what that way of working requires. A starter that reads documents a fresh repository does not have
SHALL NOT be presented as though it were portable.

#### Scenario: a prerequisite is visible before it is needed

- **WHEN** a starter requires a tool or document beyond the repository
- **THEN** that requirement is shown with the starter, not learned from a failed Run

#### Scenario: the tiers are distinguishable

- **WHEN** the set is offered
- **THEN** a starter that assumes only the repository is distinguishable from one that assumes a way
  of working

### Requirement: a starter that a project already has is reported, never replaced

Where a project has a Connector, the offer SHALL report which starters already exist at their target
path in that project's repository, so an Admin is told before copying rather than after overwriting.

An existing file SHALL always win. Since nothing is written, this holds by construction — the
reporting is what makes it useful rather than merely true.

Where a project has no Connector there is nothing to read, and the offer SHALL say so and remain
usable: looking at the set before configuring a Connector is an ordinary first step, not an error.

#### Scenario: an existing file is reported

- **WHEN** the project's repository already contains a file at a starter's target path
- **THEN** that starter is marked as already present, and nothing is written

#### Scenario: no Connector is an ordinary state

- **WHEN** the project has no Connector
- **THEN** the set is still offered, and the presence of each starter reads as unknown rather than as
  absent or as an error

### Requirement: every shipped starter loads and has a body

Every starter in the set SHALL be covered by a test asserting that it loads and that a non-empty body
remains once frontmatter is stripped by the same routine the Run path uses. A starter prompt that
fails to load is worse than none, because it is offered as working.

#### Scenario: the shipped bytes are the tested bytes

- **WHEN** the test suite runs
- **THEN** every starter the endpoint would serve is loaded and asserted to have a body after
  frontmatter is stripped
