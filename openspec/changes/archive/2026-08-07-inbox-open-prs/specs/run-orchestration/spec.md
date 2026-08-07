## ADDED Requirements

### Requirement: open changes await review in the Inbox

The Inbox SHALL show the open changes (pull requests) of every visible project's connected code
repository as a group of its own, **visually distinct from the Run waits** — a change is answered
on the vendor while a Run wait is answered inside the product, and the reader SHALL be able to tell
the two kinds apart without reading a row. Changes SHALL be ordered newest first and each SHALL
link to the vendor's page for it.

A change whose URL matches a Run's recorded output link SHALL be marked as the product's own and
SHALL link to that Run. The match SHALL be computed from what the Runs already store — no vendor
read exists to answer whose branch a change is.

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

- **WHEN** an open change's URL equals a Run's recorded output link
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
