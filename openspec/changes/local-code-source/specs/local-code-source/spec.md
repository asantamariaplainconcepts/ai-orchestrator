# local-code-source — delta

## ADDED Requirements

### Requirement: the code-source surface exists only in the self-host posture

The code-source configuration and validation endpoints SHALL be composed only when the
deployment runs in the self-host posture (the same discriminator that composes the LocalOwner
identity). A deployment without that posture MUST answer 404 for the whole surface — the option
is absent, never merely disabled — because a path input naming a cloud container's own disk is a
trap, not a feature (DEC-049).

#### Scenario: cloud deployment hides the surface

- **WHEN** a deployment composed without the self-host posture receives any code-source request
  (configure with `localFolder`, or validate-path)
- **THEN** it answers 404 and no Connector gains a local path

#### Scenario: self-host deployment offers it to Admins only

- **WHEN** a caller without Admin standing on the project calls validate-path in a self-host
  deployment
- **THEN** the request is refused by the ordinary permission gate (BR-009)

### Requirement: a local path is validated against the host before it is trusted

The product SHALL answer, for a given absolute path: whether it is a directory, whether it is a
git repository, its current branch, and whether its working tree is clean. The endpoint MUST
answer about exactly the one path it was given — it never lists directory contents.

#### Scenario: a valid repository

- **WHEN** validate-path is called with a path that is a clean git checkout on branch `main`
- **THEN** the response carries `isDirectory=true, isGitRepository=true, branch="main",
  isClean=true`

#### Scenario: the specific failure is named

- **WHEN** validate-path is called with a directory that is not a git repository
- **THEN** the response carries `isDirectory=true, isGitRepository=false` and no branch, so the
  UI can name the failing check rather than a generic error

### Requirement: a Local Run works in the folder and leaves a branch, never a push

A Run with locus Local SHALL execute in the Connector's configured folder: verify the tree is
clean, create branch `ai/{vendorStoryId}-{slug}`, hand the folder to the Agent runtime, and
commit what changed. It MUST NOT push and MUST NOT open a pull request — the output is the local
branch, recorded on the Run; `OutputLink` stays null. On a failure path the workspace SHALL
restore the previously checked-out branch.

#### Scenario: a clean tree yields a committed branch

- **WHEN** a Local Run for Story 51 executes against a clean folder and the Agent edits files
- **THEN** a branch `ai/51-…` exists locally containing those commits, nothing was pushed, and
  the Run records the branch name and the working folder

#### Scenario: the entry race is caught at execution

- **WHEN** the tree became dirty between dispatch and execution
- **THEN** the Run fails with the clean-tree sentence naming the folder, and the user's previous
  checkout is restored

### Requirement: Local Runs use the host's own credentials, and the log says so

A Local Run SHALL skip vendor-credential resolution and use whatever credentials the host's own
tooling already holds. The Run's log MUST state this in one line, so a reader never wonders which
identity touched their folder. Nothing about a local run's credentials is stored (DEC-052).

#### Scenario: no secret is resolved for a local run

- **WHEN** a Local Run executes
- **THEN** the secret-resolution seam is not invoked for the workspace, and the log carries the
  single line stating host credentials were used
