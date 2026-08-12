## ADDED Requirements

### Requirement: the backlog surfaces re-read the Mirror on a declared interval

The backlog list, the Kanban board and the Story detail view SHALL re-read their server state on a
declared interval without any user action, so an open screen converges on the Mirror's content. The
interval SHALL be declared on the query hooks in `src/frontend/features/backlog/useBacklog.ts`
(`useBacklog`, `useStory`) rather than in each screen, so every surface fed by those hooks inherits
one mechanism. The declared interval SHALL NOT exceed the server poll interval (60s, BR-015) —
a client slower than the server it mirrors would add lag without adding freshness.

#### Scenario: the list catches up unattended

- **WHEN** a Member has a project's backlog open and a Story's title changes at the vendor, and the
  server poll reconciles it into the Mirror
- **THEN** the open screen shows the new title within one client interval, with no click and no
  remount

#### Scenario: the board inherits it

- **WHEN** a Story's labels change at the vendor, the poll reconciles them, and the Member is on the
  Kanban board
- **THEN** the card appears in the new label's column within one client interval, with no click

#### Scenario: the detail view inherits it

- **WHEN** a Story's labels change at the vendor, the poll reconciles them, and the Member has that
  Story's detail view open
- **THEN** the detail shows the new labels within one client interval, with no click

### Requirement: returning to the tab re-reads immediately

A backlog surface SHALL re-read when the browser window regains focus, without waiting for the
interval to elapse. Because the application declares a global `staleTime` of 30 seconds
(`src/frontend/shared/query/queryClient.ts`), which by default suppresses a focus refetch of data
younger than that, these hooks SHALL declare freshness such that a focus event re-reads regardless
of how recently the data was fetched.

#### Scenario: focusing a background tab

- **WHEN** the backlog is open in a background tab, the Mirror has since changed, and the Member
  focuses the tab
- **THEN** the screen re-reads and shows current Mirror content without waiting for the interval

#### Scenario: focus is not suppressed by the global stale time

- **WHEN** the Member focuses the tab less than 30 seconds after the last fetch
- **THEN** a re-read is still issued — the global `staleTime` default does not suppress it

### Requirement: an automatic re-read never reaches the vendor

Every automatic re-read SHALL be served from the Mirror in Postgres. The interval and focus
re-reads SHALL call only `GET /api/projects/{id}/backlog` and
`GET /api/projects/{id}/backlog/stories/{vendorStoryId}`, which read Postgres only
(`GetBacklog.cs:65-84`). No automatic re-read SHALL call `POST /api/projects/{id}/backlog/refresh`
or any other vendor-facing path, so client freshness cannot amplify load against the single
credential (DEC-030).

#### Scenario: no vendor amplification from many tabs

- **WHEN** N tabs are open on the same project's backlog and their intervals elapse
- **THEN** the count of vendor API calls is identical to the zero-tab case

#### Scenario: the manual refresh remains the only client-initiated vendor call

- **WHEN** an automatic re-read is issued
- **THEN** it targets a Postgres-only read endpoint, and `POST /backlog/refresh` is called only in
  response to an explicit user action

### Requirement: an automatic re-read degrades to stale, never to empty

An automatic re-read SHALL preserve the existing guarantee that a failed poll reads as stale rather
than empty (`backlog-mirror`: "a failed poll degrades to stale, never to empty"). Adding a client
interval SHALL NOT introduce a path by which a Member sees an empty backlog because a vendor poll
failed.

#### Scenario: the Connector carries a failure

- **WHEN** the last server poll failed, the Connector records that failure, and an automatic
  re-read happens
- **THEN** the previously mirrored Stories are still shown and the failure is surfaced — the
  automatic re-read does not convert stale into empty

#### Scenario: empty stays distinguishable from broken

- **WHEN** automatic re-reads occur against a project whose repository genuinely has no open
  Stories
- **THEN** that state remains textually and visually distinct from a failed-poll state

### Requirement: document content stays on demand

A Story's attached documents SHALL remain live reads at the head ref, issued on demand (design D3).
Neither `useStoryDocuments` nor `useStoryDocumentContent` SHALL be placed on an interval, so
automatic freshness of Story fields never pulls document bodies.

#### Scenario: no document read is amplified

- **WHEN** a Story detail view with attached documents is open and automatic re-reads occur
- **THEN** no additional document-content read is issued

### Requirement: a hidden tab is idle and the lag is stated

While the document is hidden, no interval re-read SHALL be issued — a background tab costs nothing.
The change SHALL state its end-to-end lag budget in the DEC-050 style, as the sum of the server poll
interval and the client interval, recorded in the change's `design.md`.

#### Scenario: hidden means idle

- **WHEN** the tab is hidden and an interval would elapse
- **THEN** no re-read is issued

#### Scenario: the lag budget is stated as a sum

- **WHEN** a reader asks how far behind the vendor a shown backlog can be
- **THEN** the answer is documented as the server poll interval plus the client interval, naming
  both numbers, in the same form DEC-050 uses for the Run log window
