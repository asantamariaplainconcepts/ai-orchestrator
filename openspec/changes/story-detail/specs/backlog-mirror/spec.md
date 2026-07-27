# backlog-mirror

## ADDED Requirements

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
