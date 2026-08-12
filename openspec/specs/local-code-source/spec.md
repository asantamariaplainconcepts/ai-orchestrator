# local-code-source Specification

## Purpose
A project's code may come from a folder on the orchestrator's host (self-host flavour, DEC-049, #210): configuration and posture gating, path validation, and the local workspace whose output is a branch, never a push or a PR (BR-016).
## Requirements
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

### Requirement: Local Runs use the host's own credentials, and the log says so

A Local Run SHALL skip vendor-credential resolution and use whatever credentials the host's own
tooling already holds. The Run's log MUST state this in one line, so a reader never wonders which
identity touched their folder. Nothing about a local run's credentials is stored (DEC-052).

#### Scenario: no secret is resolved for a local run

- **WHEN** a Local Run executes
- **THEN** the secret-resolution seam is not invoked for the workspace, and the log carries the
  single line stating host credentials were used

### Requirement: a habitat that cannot reach the folder refuses by name

Where the habitat declares the Local locus unavailable, naming a `LocalFolder` code source SHALL be
refused at save, and a Run SHALL NOT resolve to the Local locus — both refusals carrying the declared
reason verbatim, never a path error from inside a container. No checkout SHALL be attempted.

The refusal SHALL exist at the API, not only in the portal: a Connector stored before the
declaration, or a request made around the portal, meets the same sentence.

#### Scenario: saving a LocalFolder Connector in a declaring habitat

- **WHEN** an Admin submits a Connector with the `LocalFolder` code source where the habitat declares
  the locus unavailable
- **THEN** the save is refused with the declared reason, and nothing is stored

#### Scenario: a pre-existing LocalFolder Connector cannot produce a Local Run

- **WHEN** a Run would resolve to the Local locus in a declaring habitat
- **THEN** the Run is refused with the declared reason — it does not fail later on a container path

#### Scenario: no checkout is attempted where the locus is unavailable

- **WHEN** a Run would resolve to the Local locus in a habitat declaring it unavailable
- **THEN** no worktree is created and no `git` command runs against any configured path

#### Scenario: the portal never offers what the habitat withheld

- **WHEN** the code-source section renders in a declaring habitat
- **THEN** the local-folder option is not offered and the reason is shown in its place

### Requirement: a Local Run works in its own checkout and leaves a branch, never a push

A Run with locus Local SHALL execute in **its own checkout of** the Connector's configured folder:
create a `git` worktree of that repository on branch `ai/{vendorStoryId}-{slug}`, hand the worktree's
path to the Agent runtime, and commit what changed. It MUST NOT push and MUST NOT open a pull
request — the output is the local branch, recorded on the Run; `OutputLink` stays null.

The configured folder MUST NOT be written to, checked out, or otherwise entered: its `HEAD`, its
current branch and its uncommitted changes SHALL be exactly as they were when the Run ends, whatever
state the Run ends in. Consequently the working tree is **not** required to be clean — neither
before dispatch nor at execution — and there is no previous checkout to restore on a failure path.

When the Run ends the worktree SHALL be removed and the branch SHALL remain in the configured
folder's repository, where its owner reaches it with ordinary `git`.

Runs with locus Local SHALL NOT be serialised against one another; the project concurrency cap
(BR-002) is the only bound on how many execute at once, as it is for sandboxed Runs.

Where the checkout cannot be created — the configured path is not a git repository, or `git` refuses
— the Run SHALL be refused before anything is written, naming the folder and the reason. Nothing
retries (BR-004).

#### Scenario: a dirty folder no longer refuses the Run

- **WHEN** a Local Run is dispatched for a Story whose Connector folder has uncommitted changes
- **THEN** the Run is accepted and executes, and no clean-tree refusal is raised at dispatch or at
  execution

#### Scenario: the owner's folder is untouched

- **WHEN** a Local Run ends, in any terminal state
- **THEN** the configured folder is on the same branch, at the same `HEAD`, with the same
  uncommitted changes it carried before the Run started

#### Scenario: a checkout yields a committed branch and then goes away

- **WHEN** a Local Run for Story 51 executes and the Agent edits files
- **THEN** a branch `ai/51-…` exists in the configured folder's repository containing those commits,
  nothing was pushed, the Run records the branch name and the checkout it worked in, and the checkout
  no longer exists

#### Scenario: two Local Runs execute at once

- **WHEN** two Local Runs for different Stories on the same configured folder are dispatched within
  the project cap
- **THEN** both execute concurrently, each in its own checkout, and neither observes the other's file
  changes

#### Scenario: an unusable folder is refused by name

- **WHEN** a Local Run is dispatched for a configured folder that is not a git repository, or where
  the worktree cannot be created
- **THEN** the Run is refused before any write, and the refusal names the folder and the specific
  reason rather than reporting a generic failure

### Requirement: checkouts abandoned by a dead process are reaped

At startup the product SHALL remove the Local-Run checkouts it created that no live Run owns, and
prune the repository's record of them. Branches those checkouts produced SHALL NOT be removed — the
branch is the Run's output and outlives the checkout by design.

The sweep exists because the measured failure mode is already on record for the sandbox substrate:
31 abandoned sandboxes and 125 GB, because a process died before its `finally` ran. A checkout leaks
the same way and for the same reason.

#### Scenario: a checkout left by a dead process is removed

- **WHEN** the product starts and finds a checkout it created for a Run that is not executing
- **THEN** the checkout is removed and the repository's worktree record is pruned

#### Scenario: reaping never destroys a Run's output

- **WHEN** an abandoned checkout carrying a committed `ai/{story}-{slug}` branch is reaped
- **THEN** the branch still exists in the configured folder's repository afterwards

#### Scenario: a live Run's checkout survives the sweep

- **WHEN** the sweep runs while a Local Run is executing
- **THEN** that Run's checkout is left alone and the Run continues

