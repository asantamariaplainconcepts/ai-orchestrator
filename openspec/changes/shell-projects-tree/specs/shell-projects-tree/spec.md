## ADDED Requirements

### Requirement: the sidebar is a tree of every visible project and its live work

The shell's navigation panel SHALL render each project the caller may see as a row linking to
`/projects/:id`, with that project's **live work** nested beneath it. A project's live work SHALL
be its **Story rows**, and each Story row SHALL nest the project's non-terminal Runs for that
Story, each linking to `/projects/:id/runs/:runId`. A Story row SHALL link to its story route.

The tree SHALL be rendered from the single navigation component in
`src/frontend/shared/ui/AppShell.tsx` that already feeds the expanded sidebar, the collapsed rail
and the mobile sheet, so no container can hold a different set of entries from another.

The Inbox entry and its ambient count SHALL be unchanged by the tree (UC-026 keeps its
subtraction semantics), and the tree SHALL introduce no change to what the Inbox contains or
counts.

#### Scenario: a project's live work is visible without navigating into it

- **WHEN** the expanded sidebar renders for a caller with a project holding a held Story and an
  executing Run
- **THEN** the project row links to `/projects/:id`, the Story row appears beneath it linking to
  its story route, and the Run appears beneath that Story linking to
  `/projects/:id/runs/:runId`

#### Scenario: the Inbox is untouched

- **WHEN** the tree renders in any state
- **THEN** the Inbox entry and its ambient count are exactly what they were before the tree
  existed

### Requirement: a Story is in the tree when it is held or carries a non-terminal Run

A Story SHALL appear under its project exactly when it carries the **hold** — the reserved label
`hitl`, decided by `StoryHold.IsHeld` in
`src/shared/AiOrchestrator.BuildingBlocks/Domain/StoryHold.cs` (BR-007, DEC-067) — **or** it has a
Run in a non-terminal state (`Queued`, `Executing`, `AwaitingInput`).

The hold SHALL be recognised case-insensitively, through `StoryHold` and nowhere else (DEC-056):
the comparison SHALL NOT be re-expressed as a database predicate, because `Story.Labels` is a
`text[]` column (`src/modules/Backlog/AiOrchestrator.Modules.Backlog/Persistence/BacklogDbContext.cs`)
whose containment operator matches case-sensitively.

Membership SHALL NOT be derived from `Story.State`. The vendor's state value is un-normalised
permanently (DEC-045, closing OPN-003), so no cross-vendor notion of an "open" or "live" issue is
available to this surface.

#### Scenario: a held Story with no Run is in the tree

- **WHEN** a Story carries the hold and has no Run
- **THEN** it appears under its project with no Run nested beneath it

#### Scenario: the hold folds case

- **WHEN** a Story carries the hold spelled `HITL`
- **THEN** it appears in the tree exactly as one spelled `hitl` does

#### Scenario: a Story running without a hold is in the tree

- **WHEN** a Story carries no hold and has a `Queued` Run
- **THEN** it appears under its project with that Run nested beneath it

#### Scenario: vendor state is not a membership test

- **WHEN** a Story is open in the vendor but carries no hold and has no non-terminal Run
- **THEN** it does not appear in the tree

### Requirement: a project with no live work renders as its row alone

A project with no held Story and no non-terminal Run SHALL render as its project row and nothing
else — no empty group, no placeholder row, and no count of zero.

#### Scenario: a quiet project

- **WHEN** a visible project has no held Story and no non-terminal Run
- **THEN** only its project row renders

#### Scenario: no projects at all

- **WHEN** the caller can see no projects
- **THEN** the tree renders the same entry point to the projects list the sidebar offers today

### Requirement: work leaves the tree by derivation, never by a signal

Tree membership SHALL be computed at read time from the Runs' states and the Stories' labels. The
system SHALL store nothing about the tree: no flag, no cached membership, no per-entry timestamp.

A Run reaching a terminal state SHALL leave the tree at the next refresh because it no longer
satisfies the membership predicate. No new transport SHALL be introduced for this — no websocket,
no server-push channel — and no second polling cadence: the tree SHALL refresh on the cadence the
shell's existing Inbox query already runs.

#### Scenario: a finished Run leaves

- **WHEN** a Run reaches `Succeeded`, `Failed` or `Cancelled` and the next refresh completes
- **THEN** its row is absent from the tree, and nothing was written to make it so

#### Scenario: a cleared hold leaves

- **WHEN** a person removes the hold from a Story that has no non-terminal Run
- **THEN** that Story's row is absent at the next refresh

#### Scenario: no new channel

- **WHEN** the tree is rendered and refreshed
- **THEN** it uses the shell's existing polling cadence, and no websocket or push channel exists
  for it

### Requirement: the tree never reveals a project its caller may not see

The tree SHALL be scoped by the same project visibility every other cross-project surface uses
(BR-009). A project the caller may not see SHALL be **absent** from the tree — not present and
empty, and not present with its children withheld.

#### Scenario: an invisible project is absent

- **WHEN** a caller who may see one of two projects renders the tree, and both have live work
- **THEN** only the visible project appears, and the response carries no identifier, name, or
  Story title belonging to the other

#### Scenario: the tree agrees with the projects list

- **WHEN** the tree and the projects list are compared for one caller
- **THEN** the tree contains no project the projects list would not show

### Requirement: every width offers the same entries and the same destinations

The collapsed icon rail SHALL keep every project present as a glyph, and opening a project SHALL
reveal the **same** children with the **same** destinations as the expanded tree (#126 design D2 —
the rail drops the label, never the entry). The mobile sheet SHALL render the same entries,
inline rather than in a flyout.

A collapsed entry SHALL carry its name for assistive technology and on hover, since the label is
no longer rendered.

#### Scenario: the rail keeps every project

- **WHEN** the sidebar is collapsed to the rail with three visible projects
- **THEN** all three are present as glyphs, each carrying its name to assistive technology and on
  hover

#### Scenario: the rail's children match the expanded tree

- **WHEN** a project is opened from the rail
- **THEN** the children revealed and their destinations are identical to that project's children
  in the expanded tree

#### Scenario: the sheet renders the same entries

- **WHEN** the mobile sheet opens
- **THEN** it renders the same projects, Stories and Runs with the same destinations, inline

### Requirement: the tree is built from the design system and the copy catalogue

Every string the tree renders SHALL resolve through the typed i18n catalogue (DEC-021 — hardcoded
JSX copy fails CI). The surface SHALL use Platform-theme tokens and kit primitives only (DEC-051,
`DESIGN.md`), and SHALL introduce no raw hex colour or raw pixel value.

#### Scenario: no hardcoded copy

- **WHEN** `pnpm lint` runs over the tree's components
- **THEN** it passes at `--max-warnings=0`, with every user-facing string resolved from the
  catalogue

#### Scenario: the design validator passes

- **WHEN** the design validator runs over the tree's components
- **THEN** no raw hex colour, raw pixel value, or non-approved font is found
