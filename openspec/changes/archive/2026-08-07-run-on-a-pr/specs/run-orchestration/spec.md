## ADDED Requirements

### Requirement: a Run can target an open change with an ad-hoc instruction

A Member SHALL be able to launch a Run against an open change (pull request) of a project's
connected repository, supplying a non-empty instruction typed at launch. The Run SHALL work on the
change's **existing head branch** and push to it, so the same change updates in place — it SHALL
NOT open a new change or a new branch. The permission SHALL be the one that allows Run now
(UC-012), and there SHALL be no approval phase: the launch is the human intent, which is UC-012's
own reasoning.

Such a Run has no Story and no Automation, and that SHALL be a stated shape rather than a
workaround: a Run targets exactly one of a Story or a change, never both and never neither. Cost
and usage SHALL land on the project exactly as any Run's (BR-011 unchanged). The Runs surfaces
SHALL identify a change-targeted Run by its change number the way story Runs are identified by
their Story id, and a surface that groups Runs by Story SHALL tolerate a Run that has none.

The change's URL and head branch SHALL be resolved from the vendor at launch, never taken from
the caller: the caller names a change number, and what that number means is the vendor's answer
(BR-008). A number naming no open change SHALL be refused.

**One active Run per change:** while a Run targeting a change is active, a second launch against
the same change SHALL be refused with a reason naming the active Run, enforced the way BR-001 is —
a filtered unique constraint as the authority, with the pre-check and the race arm converging on
the same refusal. A Story Run and a change Run SHALL NOT contend.

The instruction SHALL be recorded on the Run and readable in its detail afterwards, and SHALL NOT
create or modify any Automation: ad-hoc text creates a Run, not configuration.

#### Scenario: the change updates in place

- **WHEN** a Member launches a Run on an open change with an instruction and it succeeds
- **THEN** the Run's commits are on the change's existing head branch, the change shows them, and
  no new change or branch exists

#### Scenario: one at a time per change

- **WHEN** a second launch targets a change whose Run is still active
- **THEN** it is refused with a reason naming the active Run, and nothing is created

#### Scenario: a Story Run does not block a change Run

- **WHEN** a Story has an active Run and a Member launches a Run on an unrelated open change
- **THEN** the change Run starts

#### Scenario: the instruction is a record, not configuration

- **WHEN** a change-targeted Run completes and an Admin reads the project's Automations
- **THEN** nothing new exists there, and the Run's detail shows the instruction it ran

#### Scenario: an empty instruction is refused at the edge

- **WHEN** a launch arrives with an empty or whitespace instruction
- **THEN** it is refused by validation and no Run exists

#### Scenario: a number the vendor does not answer is refused

- **WHEN** a launch names a change number that is not among the repository's open changes
- **THEN** it is refused with a reason and no Run exists

#### Scenario: failure leaves a Run failure, not a mystery

- **WHEN** the Agent cannot complete the instruction, or the push is refused
- **THEN** the Run fails with the stage-named reason in its record, and the change holds only
  whatever was already pushed

## MODIFIED Requirements

### Requirement: open changes await review in the Inbox

The Inbox SHALL show the open changes (pull requests) of every visible project's connected code
repository as a group of its own, **visually distinct from the Run waits** — a change is answered
on the vendor while a Run wait is answered inside the product, and the reader SHALL be able to tell
the two kinds apart without reading a row. Changes SHALL be ordered newest first and each SHALL
link to the vendor's page for it.

A change the product's own work produced SHALL be marked as the product's own and SHALL link to
its Run. The match SHALL be computed from what the Runs already store, never asked of the vendor:
a change whose head branch is the Run ceremony's own (`run/<id>`) belongs to the Run whose id the
branch carries, and a change a change-targeted Run updates is that Run's recorded target. Matching
on a Run's recorded output link is not available — the publish step that wrote it was retired
(DEC-062) and no Run has carried one since.

The list SHALL be read live and never stored (BR-008): a change merged or closed on the vendor is
gone on the next read. A vendor refusal SHALL degrade to a readable reason in the group's place
while the Run waits render as always, and a project with no connected code repository SHALL simply
contribute nothing.

The shell's ambient count SHALL keep meaning what it means today — Runs waiting on a human — and
SHALL NOT trigger the vendor read: the count polls from every page on a fast cadence, and a
per-project vendor read on that cadence would spend the rate limit (the seam's own polling
requirement) on a number nobody asked for. The changes are read only while the Inbox itself is
open, on a slower cadence than the Run waits.

#### Scenario: the group is distinct and ordered

- **WHEN** a Member opens the Inbox while a visible project's repository has open changes
- **THEN** the changes render as their own visually distinct group, newest first, each linking to
  the vendor

#### Scenario: the product's own changes say so

- **WHEN** an open change's head branch is `run/<id>` for a Run of that project
- **THEN** the entry is marked as created by the product and links to that Run

#### Scenario: the vendor stays the truth

- **WHEN** a change is merged or closed on the vendor
- **THEN** the next read no longer lists it and nothing about it was stored

#### Scenario: refusal degrades beside working waits

- **WHEN** the vendor read fails for a project
- **THEN** the Run waits render as always and the changes group shows a readable reason

#### Scenario: the ambient count is unmoved

- **WHEN** open changes exist for visible projects
- **THEN** the shell's count still counts only Runs waiting on a human, and rendering it performs
  no vendor read
