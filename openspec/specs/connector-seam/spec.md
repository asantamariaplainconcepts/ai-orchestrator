# connector-seam Specification

## Purpose
TBD - created by archiving change github-connector-backlog-mirror. Update Purpose after archive.
## Requirements
### Requirement: vendors are reached through one abstraction

All vendor access SHALL go through a single connector abstraction exposing verification and Story
retrieval. Vendor SDK types SHALL NOT appear outside the implementation that owns them, so a
second vendor can be added without touching any caller.

#### Scenario: adding a vendor

- **WHEN** a second vendor implementation is added
- **THEN** the polling loop, the mirror, and the API are unchanged

#### Scenario: no SDK leakage

- **WHEN** code outside the GitHub implementation is inspected
- **THEN** no GitHub SDK type appears in a signature, a domain type, or an API contract

### Requirement: the Backlog module owns its data and references projects by identity

The Backlog module SHALL own the Connector and Story data and SHALL reference a Project by its
identifier only. It SHALL NOT reference another module's implementation assembly.

#### Scenario: module boundaries hold

- **WHEN** the solution is built and the architecture tests run
- **THEN** no cross-module implementation reference exists, and no `.Contracts` assembly is
  required for this change

#### Scenario: schema ownership

- **WHEN** the Backlog module's migrations run
- **THEN** its tables land in its own schema and no other module's schema is altered

### Requirement: polling stays within the vendor's rate limit

The connector SHALL poll at a rate that leaves substantial headroom against the vendor's limit,
and SHALL surface rate-limit exhaustion as a distinct, recorded failure rather than as a generic
error.

Conditional requests are **not** used, for a reason discovered during implementation and recorded
rather than worked around: Octokit 14's high-level API can read a response's ETag but provides no
way to send `If-None-Match`, so it cannot make a conditional request at all. At the default
interval one project consumes roughly 60–120 of 5000 requests per hour, so the optimisation buys
nothing today. It becomes worth revisiting — via a custom HTTP layer, or a different client — when
the number of projects makes the arithmetic tight.

#### Scenario: the rate limit is exhausted

- **WHEN** a poll fails because the vendor's rate limit is exhausted
- **THEN** that is recorded against the Connector as its own failure reason, distinguishable from
  an unreachable vendor or a rejected credential

### Requirement: the Connector can find a Story's linked change and read its documents

The Connector seam SHALL expose, in vendor-neutral vocabulary, the ability to find the change
linked to a Story (number, title, URL, head ref) and to read a document's content at a ref. No
vendor noun SHALL appear in the seam's types — a second vendor implements the same two reads
against its own model (work-item relations rather than issue cross-references). Failures SHALL
reuse the existing closed error set so the API's problem codes stay finite.

#### Scenario: the linked change is found through the seam

- **WHEN** a Story has a change that references it
- **THEN** the Connector reports that change with its head ref, through types carrying no
  vendor-specific name

#### Scenario: a Story with no linked change

- **WHEN** no change references the Story
- **THEN** the Connector reports its absence — distinctly from a vendor failure

### Requirement: the Connector reports a change's file changes with their patches

The seam SHALL expose the files a change touches — path, status, added and removed line counts,
and the vendor's unified patch — in vendor-neutral types. When a patch is unavailable the file
SHALL carry an explicit reason (binary content, or a patch beyond the size bound) rather than an
empty or truncated patch. The documents list (UC-023) SHALL be a projection of this same read,
not a second vendor call.

#### Scenario: the changed files are reported with their diffs

- **WHEN** a change touching text files is read through the seam
- **THEN** each file reports its path, status, counts and unified patch

#### Scenario: a patch that cannot be shown says why

- **WHEN** a file is binary, or its patch exceeds the bound
- **THEN** the file reports the reason and carries no patch — never a truncated one

### Requirement: the Connector can comment on a Story and change its state

The seam SHALL expose adding a comment to a Story and setting a Story's state, in vendor-neutral
terms. A state the vendor does not accept SHALL be refused with a stated reason rather than
guessed at or silently ignored. Both SHALL reuse the existing error taxonomy.

#### Scenario: a comment reaches the vendor

- **WHEN** a comment is added through the seam
- **THEN** the vendor's Story carries it

#### Scenario: an unknown state is refused

- **WHEN** a transition names a state the vendor does not accept
- **THEN** the write is refused, naming the state, and nothing changes

### Requirement: the seam is normalised, and state values are the deliberate exception

The abstraction SHALL express Stories in the product's own vocabulary — id, title, state, labels
— rather than mirroring any one vendor's schema. Vendor-specific mapping SHALL live in that
vendor's implementation.

State values SHALL be carried through verbatim from whichever vendor produced them, and the
product SHALL NOT define a canonical state vocabulary. With two real vendors implemented this is
settled rather than deferred (DEC-045): a GitHub repository and an Azure DevOps process template
each own their own state names, and any set the product invented would name states that no board
has and force every write to guess a translation.

#### Scenario: vendors disagree about vocabulary

- **WHEN** a vendor calls its concepts something other than Story, state or label
- **THEN** the mapping happens inside that vendor's implementation, and the rest of the system
  sees the product's vocabulary (DEC-005)

#### Scenario: state values stay the vendor's own

- **WHEN** a Story's state is read from either vendor
- **THEN** that vendor's own state value is carried through unaltered

#### Scenario: a state the vendor will not accept

- **WHEN** a state is written that the vendor's process does not allow
- **THEN** the vendor's refusal is surfaced naming what was attempted, rather than being mapped
  onto some nearest product state

### Requirement: a label can be ensured in the repository, not only on a Story

The seam SHALL expose a way to ensure a label exists in a connector's repository, independent of
any Story. It SHALL succeed when the label already exists, so callers need not distinguish
"created" from "was already there". Where a vendor has no repository-level concept of a label,
the implementation SHALL succeed without acting rather than simulate one by labelling an
arbitrary work item, and SHALL say so where a reader will find it.

#### Scenario: the label is absent

- **WHEN** a label is ensured in a repository that does not have it
- **THEN** it exists afterwards and no Story has been modified

#### Scenario: the label is already there

- **WHEN** a label that already exists is ensured
- **THEN** the call succeeds and nothing changes

#### Scenario: a vendor with no repository-level labels

- **WHEN** a label is ensured against a vendor whose tags only exist once applied to a work item
- **THEN** the call succeeds without creating or modifying anything

