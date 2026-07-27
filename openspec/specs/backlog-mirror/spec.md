# backlog-mirror Specification

## Purpose
TBD - created by archiving change github-connector-backlog-mirror. Update Purpose after archive.
## Requirements
### Requirement: the vendor is the source of truth and the mirror is a projection

Stories SHALL be persisted as a read model of what the vendor holds — vendor id, title, state,
labels, and the time last seen (DEC-029). The application SHALL NOT treat the mirror as
authoritative, and SHALL NOT modify a Story except by re-reading it from the vendor (BR-008).

#### Scenario: a Story changes at the vendor

- **WHEN** a Story's title, state or labels change in the repository and a poll runs
- **THEN** the mirror reflects the new values, and no duplicate Story is created

#### Scenario: a Story leaves the repository

- **WHEN** a Story that was mirrored is closed or deleted at the vendor and a poll runs
- **THEN** the mirror reflects its absence rather than retaining it as current

#### Scenario: identity survives a rename

- **WHEN** a Story's title changes
- **THEN** it remains the same Story, because identity is the vendor's id and never the title

### Requirement: a poll is a full reconciliation

Each poll SHALL fetch the repository's current open Stories and reconcile the mirror against that
result in full — upserting what is present and marking absent what is not.

#### Scenario: repeated polls are idempotent

- **WHEN** two polls run with nothing changed at the vendor
- **THEN** the mirror is identical after the second, with no duplicates and no churn

### Requirement: polling runs on a schedule and on demand

The system SHALL poll each configured Connector on its project's interval, defaulting to 60
seconds (BR-015, DEC-028), and SHALL additionally expose an explicit refresh that polls
immediately without waiting for the interval.

#### Scenario: scheduled polling

- **WHEN** the application is running with a configured Connector
- **THEN** polls occur on the configured interval without user action

#### Scenario: on-demand refresh

- **WHEN** a user triggers a refresh
- **THEN** a poll runs immediately and the mirror reflects the result

#### Scenario: a project with no Connector

- **WHEN** the poller reaches a Project that has no Connector
- **THEN** it skips that Project without error

### Requirement: a failed poll degrades to stale, never to empty

When a poll fails — the vendor is unreachable, rate-limited, or rejects the credential — the
previously mirrored Stories SHALL remain readable, and the failure SHALL be recorded against the
Connector so it can be surfaced. A failure SHALL NOT empty the mirror and SHALL NOT be silent.

#### Scenario: the vendor is unreachable

- **WHEN** a poll fails to reach the vendor
- **THEN** previously mirrored Stories are still returned to clients, and the failure is recorded
  with its time and reason

#### Scenario: distinguishing empty from broken

- **WHEN** a client views a backlog that has no Stories
- **THEN** it can tell "the repository has no open Stories" from "the last poll failed", because
  the two are represented differently

### Requirement: the backlog is visible in the application

A Project's page SHALL show its Connector configuration and its mirrored Stories, and SHALL handle
the empty, loading and error states defined by the design system.

#### Scenario: viewing a connected project

- **WHEN** a user opens a Project that has a Connector and mirrored Stories
- **THEN** the Connector's coordinates and the Stories are shown, with vendor ids in monospace

#### Scenario: viewing an unconnected project

- **WHEN** a user opens a Project with no Connector
- **THEN** the page states that no backlog is connected and offers to configure one

### Requirement: a Member applies or removes a trigger label through the Connector

The system SHALL let a Member apply or remove a label on a mirrored Story via
`PUT`/`DELETE /api/projects/{projectId}/backlog/stories/{vendorStoryId}/labels/{label}`. The
write SHALL go to the vendor through the Connector seam **before** the mirror changes, and the
mirror SHALL then be re-synchronised through the same reconciliation path polling uses — so
portal labelling and vendor labelling are one mechanism (DEC-027) and the resulting
`StoryChanged` event drives matching identically. A vendor-rejected write SHALL surface its
distinct error and leave the mirror untouched. Both operations SHALL be idempotent. The portal
SHALL offer apply/remove for enabled Automations' trigger labels and render other labels
read-only.

#### Scenario: the portal drives the loop

- **WHEN** a Member applies an enabled Automation's trigger label to a Story from the backlog
  page
- **THEN** the vendor receives the label, the re-synchronised mirror shows it, and a Run is
  created by the ordinary matching path

#### Scenario: removal writes back the same way

- **WHEN** the Member removes a trigger label they applied
- **THEN** the vendor no longer has the label and the mirror, once re-synchronised, agrees

#### Scenario: the vendor refuses

- **WHEN** the vendor rejects the write (unavailable or permission)
- **THEN** the API returns the vendor's distinct error and the mirrored Story is unchanged

#### Scenario: idempotence follows HTTP

- **WHEN** the same PUT is repeated, or a DELETE targets a label the Story does not carry
- **THEN** the outcome equals the single application / a successful no-op

### Requirement: the Mirror holds the Story's description and the portal renders it

Reconciliation SHALL mirror the vendor's issue body onto the Story and SHALL count it in the
change detection, so an edited description updates the Mirror on the next poll and announces a
`StoryChanged` like any other change (BR-008, DEC-028). The portal SHALL offer a Story detail
view reached from the backlog showing vendor id, title, state, labels and the body rendered as
markdown, read through its own endpoint rather than by widening the backlog list. Rendering
SHALL be sanitised: no raw HTML, no scripts, no `javascript:` URLs.

#### Scenario: the description is mirrored and rendered

- **WHEN** a Story whose issue has a description is refreshed and its detail view opened
- **THEN** the Mirror holds the body and the page renders it as markdown

#### Scenario: an edited description is a change

- **WHEN** the description is edited at the vendor and the next poll runs
- **THEN** the Mirror holds the new text and the poll counted it as a change

#### Scenario: no description

- **WHEN** a Story has no description
- **THEN** the detail view shows the documented empty state, not a blank region

#### Scenario: hostile markdown is inert

- **WHEN** a body contains a `<script>` tag or a `javascript:` link
- **THEN** no script executes and the link does not navigate

### Requirement: a Story's attached documents are readable in the portal

The Story detail view SHALL list the markdown documents the Story's linked change adds or
modifies, and render the selected one through the same sanitising pipeline as the description.
Documents SHALL be read live at the change's head ref and SHALL NOT be mirrored (BR-008): a
branch that has moved on shows its current content. The view SHALL distinguish three absences —
no linked change, a change with no documents, and a document that could not be read.

#### Scenario: the specification is readable in the portal

- **WHEN** a Story's linked change adds markdown documents and the detail view is opened
- **THEN** the documents are listed by path and the selected one renders

#### Scenario: the branch moved on

- **WHEN** a document is read after its branch advanced
- **THEN** the content is the branch's current head, not an earlier copy

#### Scenario: three absences, three messages

- **WHEN** there is no linked change, or the change adds no documents, or a read fails
- **THEN** the view says which of the three it is

#### Scenario: document content is untrusted too

- **WHEN** a document contains a script or raw HTML
- **THEN** nothing executes — the same pipeline the description uses

### Requirement: a verified vendor webhook triggers the same reconciliation a poll does

The system SHALL accept vendor webhooks at a public endpoint, verify the request's signature
against the Connector's configured secret using a constant-time comparison, and — for an
interesting event — run the same reconciliation the poller runs, so the resulting story events
are produced by one code path and are identical whatever prompted them (BR-015). The payload
SHALL NOT be translated into a story event. An unsigned or wrongly signed request, and one
naming a repository no Connector watches, SHALL be refused indistinguishably (no existence
leak). An uninteresting event SHALL be acknowledged without work. Polling SHALL continue
regardless, so a missed webhook costs latency and never correctness. The webhook secret SHALL
be held by name (BR-010).

#### Scenario: a signed webhook reconciles

- **WHEN** a correctly signed event arrives for a watched repository
- **THEN** the mirror is reconciled exactly as a poll would reconcile it, and any story event
  is indistinguishable from a poll's

#### Scenario: an unsigned or wrongly signed request is refused

- **WHEN** the signature is absent or wrong
- **THEN** the request is refused and no reconciliation happens

#### Scenario: an unknown repository leaks nothing

- **WHEN** the payload names a repository no Connector watches
- **THEN** the answer is the same as a signature failure

#### Scenario: an uninteresting event is accepted and ignored

- **WHEN** an event the product does not act on arrives
- **THEN** the response is success and no reconciliation happens

#### Scenario: polling still reconciles without webhooks

- **WHEN** a Story changes and no webhook arrives
- **THEN** the next poll reconciles it as before

