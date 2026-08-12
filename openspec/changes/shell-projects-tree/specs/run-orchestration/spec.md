## MODIFIED Requirements

### Requirement: cross-module reads happen through the second and third Contracts surfaces

The Runs module SHALL read Automations through `IAutomationCatalog` in
`AiOrchestrator.Modules.Projects.Contracts` and Stories through `IStoryReader` in
`AiOrchestrator.Modules.Backlog.Contracts`. The owning modules SHALL register the
implementations. The Runs module SHALL reference no other module's implementation assembly and
no messaging or cloud SDK — the existing guardrail suite SHALL verify it with these assemblies
in place.

`IStoryReader` SHALL additionally answer **which of a project's Stories are held** — the reserved
label `hitl` (BR-007, DEC-067) — carrying each held Story's vendor id and title. A consumer SHALL
NOT have to enumerate every vendor story id and then read each snapshot to learn this: that is one
round trip per Story in the mirror, and the cross-project tree asks the question for every visible
project on the shell's polling cadence.

The held test SHALL be `StoryHold.IsHeld`
(`src/shared/AiOrchestrator.BuildingBlocks/Domain/StoryHold.cs`) and SHALL NOT be re-expressed as
a database predicate. `Story.Labels` is a `text[]` column
(`src/modules/Backlog/AiOrchestrator.Modules.Backlog/Persistence/BacklogDbContext.cs`) whose
containment operator is case-sensitive, so a SQL-side test would report a Story labelled `HITL` as
unheld — the exact failure DEC-056's case-folding exists to prevent, and a second home for that
rule is how the two copies would drift apart.

#### Scenario: the boundary holds with three modules

- **WHEN** the guardrail suite runs with the Runs module present
- **THEN** implementation references between modules still fail, Contracts references pass,
  and the Runs module carries no infrastructure reference

#### Scenario: held Stories are one read, not one per Story

- **WHEN** a consumer asks which of a project's Stories are held
- **THEN** it receives the held Stories with their titles from a single read, without enumerating
  the project's every vendor story id

#### Scenario: the fold has one home

- **WHEN** a project's mirror holds Stories labelled `hitl`, `HITL` and `Hitl`
- **THEN** all three are reported held, decided by `StoryHold.IsHeld` rather than by a database
  containment test

### Requirement: Runs are observable per project and per Story

The system SHALL expose a project's Runs read-only at
`GET /api/projects/{projectId}/runs`, newest first, with an optional `vendorStoryId` filter
for the per-Story view (UC-021, DEC-031). Each Run SHALL expose exactly the BR-014 subset it
records today: id, vendor story id, automation id, state, created timestamp, dispatched
timestamp. The portal SHALL render the project's Runs and a per-Story filter reachable from
the backlog, joining automation details client-side from the automations endpoint; fields
DEC-031 names that have no producing feature yet (output link, logs, cost) SHALL render the
design system's empty value, and a project without Runs SHALL show the empty state.

The system SHALL additionally expose a **cross-project in-flight read** at `GET /api/in-flight`
(UC-033) — a third observation surface beside the per-project list and the Inbox. It SHALL report,
for every project the caller may see, that project's held Stories and its Runs in a non-terminal
state (`Queued`, `Executing`, `AwaitingInput`), each Run carrying the Story it belongs to so a Run
is never reported without the work it is doing.

It SHALL be scoped exactly as the Inbox is: by `IProjectPermissions.VisibleProjects`, with
`null` meaning all and resolved through `IProjectCatalog.ActiveProjectIds` where an enumerable
scope is needed (BR-009). A project the caller may not see SHALL be absent from the response, not
present and empty.

It SHALL read only the local read models — the Runs' own tables and the Postgres Mirror — and SHALL
make no vendor call, because it is polled from every portal page. This is the constraint that
separates it from the open-changes surface, whose per-project vendor read is precisely why that
surface is not folded into a shell-cadence poll.

It SHALL NOT be folded into `GET /api/inbox`. The shell's ambient count is the length of the
Inbox's array, so an entry that is not a Run waiting on a human would corrupt a count UC-026
defines.

#### Scenario: a member sees what the loop produced

- **WHEN** a Member opens the Runs view of a project where matching has created Runs
- **THEN** each Run lists its Story reference, its Automation's trigger/action/runtime, its
  state and its timestamps, newest first

#### Scenario: the per-Story view isolates one Story's history

- **WHEN** the Member follows a backlog row to its Runs
- **THEN** only Runs whose vendor story id matches that Story are listed

#### Scenario: absent data is shown as absent

- **WHEN** a Run's output link, logs or cost have no producing feature, or its Automation no
  longer exists in current configuration
- **THEN** those cells render the design system's empty value — never a blank, a zero, or an
  invented value

#### Scenario: no Runs yet

- **WHEN** a Member opens the Runs view of a project where nothing has ever matched
- **THEN** the design-system empty state explains that Runs appear when an Automation matches

#### Scenario: in-flight work across projects in one read

- **WHEN** a caller with two visible projects requests the in-flight read, one project holding a
  held Story and the other an `Executing` Run
- **THEN** both projects are reported, each with its own live work, and every reported Run names
  the Story it belongs to

#### Scenario: terminal Runs are not in flight

- **WHEN** a project's only Runs are `Succeeded`, `Failed` or `Cancelled`
- **THEN** the in-flight read reports no Runs for it, including the failures the Inbox still shows

#### Scenario: an invisible project is absent from the in-flight read

- **WHEN** a caller who may see one of two projects requests the in-flight read and both have
  live work
- **THEN** the response carries only the visible project, with no id, name, or Story title from
  the other

#### Scenario: the in-flight read makes no vendor call

- **WHEN** the in-flight read is served for a project whose Connector is failing or absent
- **THEN** it answers from the local read models, and the Connector's state neither blocks it nor
  appears in it

#### Scenario: the ambient count is unchanged

- **WHEN** the in-flight read reports a `Queued` Run and a held Story for a project
- **THEN** `GET /api/inbox` returns exactly what it returned before, and the shell's count is
  unchanged
