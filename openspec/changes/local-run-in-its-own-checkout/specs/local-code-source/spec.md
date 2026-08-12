## MODIFIED Requirements

### Requirement: a Local Run works in the folder and leaves a branch, never a push

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

## ADDED Requirements

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
