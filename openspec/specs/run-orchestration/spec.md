# run-orchestration Specification

## Purpose
TBD - created by archiving change story-automation-matching. Update Purpose after archive.
## Requirements
### Requirement: a matching story event creates a Run and dispatches it

When a `StoryChanged` event (Added or Updated) is handled and the Story's current labels and
state match an enabled Automation of its Project with `requiresApproval = false`, and no rule
refuses, the system SHALL create a Run recording the story reference, the Automation, its
creation timestamp and its state (BR-014 subset), and SHALL enqueue exactly one dispatch
message carrying the Run id (BR-007 single-phase). Matching SHALL read the Story and the
Automations through Contracts read interfaces — current truth, never the event payload
(BR-015). A Removed event SHALL never match.

#### Scenario: the loop closes

- **WHEN** a Story gains the trigger label of an enabled single-phase Automation and the
  `StoryChanged` event is handled
- **THEN** a Run exists in `Queued` referencing that Story and Automation, and one dispatch
  message carrying the Run id is on the queue

#### Scenario: no matching Automation

- **WHEN** an event is handled for a Story matching no enabled Automation of its Project
- **THEN** no Run is created and nothing is enqueued

#### Scenario: the two-phase lane is refused, loudly

- **WHEN** the matching Automation has `requiresApproval = true`
- **THEN** no Run is created, and the refusal is logged naming the Automation — this slice's
  stated limitation, not silence

### Requirement: one active Run per Story is a database constraint

BR-001 SHALL be enforced by a partial unique index over the Run's story reference across the
active states (`Queued`, `Planning`, `AwaitingApproval`, `Executing`). A match against a Story
that already has an active Run SHALL be ignored — no new Run, nothing enqueued, not queued for
later. The handler SHALL treat the index violation as "already done", never as an error.

#### Scenario: a second match while a Run is active

- **WHEN** a matching event is handled for a Story whose Run is in an active state
- **THEN** no second Run exists and no message was enqueued

#### Scenario: concurrent handling of the same Story

- **WHEN** two deliveries for the same Story are handled concurrently
- **THEN** exactly one Run exists afterwards — the index decides the race, and the loser
  reports success

### Requirement: duplicate delivery changes nothing

Delivery is at-least-once; the handler SHALL be idempotent. Handling the same `StoryChanged`
twice SHALL produce the same outcome as handling it once: one Run, one dispatch message.

#### Scenario: the same event delivered twice

- **WHEN** an identical `StoryChanged` is delivered a second time while the created Run is
  active
- **THEN** no second Run and no second dispatch message exist

### Requirement: the project cap holds at creation

BR-002 SHALL be evaluated when a Run is created: if the Project already has as many Runs in
`Planning`/`Executing` as its cap (default 2), the new Run SHALL remain `Queued` and no
dispatch message SHALL be enqueued. Promotion when capacity frees is explicitly out of this
slice — nothing can complete yet.

#### Scenario: a match at the cap

- **WHEN** a match occurs while the Project has cap-many Runs in `Planning`/`Executing`
- **THEN** the Run exists in `Queued` and the queue received nothing

### Requirement: cross-module reads happen through the second and third Contracts surfaces

The Runs module SHALL read Automations through `IAutomationCatalog` in
`AiOrchestrator.Modules.Projects.Contracts` and Stories through `IStoryReader` in
`AiOrchestrator.Modules.Backlog.Contracts`. The owning modules SHALL register the
implementations. The Runs module SHALL reference no other module's implementation assembly and
no messaging or cloud SDK — the existing guardrail suite SHALL verify it with these assemblies
in place.

#### Scenario: the boundary holds with three modules

- **WHEN** the guardrail suite runs with the Runs module present
- **THEN** implementation references between modules still fail, Contracts references pass,
  and the Runs module carries no infrastructure reference

### Requirement: Runs are observable per project and per Story

The system SHALL expose a project's Runs read-only at
`GET /api/projects/{projectId}/runs`, newest first, with an optional `vendorStoryId` filter
for the per-Story view (UC-021, DEC-031). Each Run SHALL expose exactly the BR-014 subset it
records today: id, vendor story id, automation id, state, created timestamp, dispatched
timestamp. The portal SHALL render the project's Runs and a per-Story filter reachable from
the backlog, joining automation details client-side from the automations endpoint; fields
DEC-031 names that have no producing feature yet (output link, logs, cost) SHALL render the
design system's empty value, and a project without Runs SHALL show the empty state.

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

### Requirement: a Member dispatches a Run on demand

The system SHALL let a Member create a Run for a chosen Story and enabled Automation via
`POST /api/projects/{projectId}/runs` (UC-012). The request SHALL take the same creation path
as event matching — BR-001, BR-002 and the BR-007 lane split enforced by the same code — and
SHALL bypass only trigger detection (BR-013): the Story need not carry the trigger label.
Refusals SHALL answer the human: an active Run yields a conflict naming BR-001; a two-phase
Automation yields the stated limitation; an unknown Story or unavailable Automation yields a
distinct validation error and nothing is written. At the BR-002 cap the Run SHALL be created
`Queued`, nothing enqueued, and the response SHALL say so.

#### Scenario: run now without the label

- **WHEN** a Member triggers Run now for a Story that does not carry the Automation's trigger
  label
- **THEN** a Run exists and one dispatch message carries its id — identical in shape to a
  matched event's Run

#### Scenario: the rules answer instead of ignoring

- **WHEN** Run now targets a Story with an active Run
- **THEN** the response is a conflict naming the one-active-Run rule and no Run was created

#### Scenario: the cap speaks

- **WHEN** Run now fires while the Project is at its concurrency cap
- **THEN** the Run exists in `Queued`, the queue received nothing, and the response states the
  Run is waiting

#### Scenario: the gate is not a bypass

- **WHEN** Run now targets a `requiresApproval = true` Automation
- **THEN** the request is refused with the two-phase stated limitation and nothing is written

### Requirement: an approval-gated Run pauses on its Plan and a human decides

A Run whose Automation has `requiresApproval = true` SHALL produce a Plan, store it on the Run
and pause at `AwaitingApproval` without publishing anything (BR-007, DEC-040). Approving SHALL
stamp the approval, return the Run to `Queued` and re-enqueue it for execution; rejecting SHALL
end the Run `Cancelled` — terminal, freeing the Story (BR-001). A Run awaiting approval SHALL
be subject to no timeout (BR-006) and SHALL NOT count toward the project cap (BR-002), while
still holding its Story against a second Run (BR-001). The Plan and the decision SHALL be part
of the Run's record (BR-014). No code path SHALL any longer refuse the two-phase lane as
unimplemented.

#### Scenario: the Agent proposes and the Run waits

- **WHEN** an approval-gated Run executes
- **THEN** its Plan is stored, its state is `AwaitingApproval`, and no branch or pull request
  was created

#### Scenario: approval resumes into execution

- **WHEN** the Plan is approved
- **THEN** the Run is re-enqueued, executes the implement path, and ends `Succeeded` with a
  pull request — as the single-phase lane does

#### Scenario: rejection ends it

- **WHEN** the Plan is rejected
- **THEN** the Run ends `Cancelled`, nothing is enqueued, and the Story can run again

#### Scenario: waiting is free and untimed

- **WHEN** a Run sits in `AwaitingApproval`
- **THEN** no timeout applies to it and the project's concurrency cap is unaffected, yet a new
  match on the same Story still creates no second Run

### Requirement: a Run's detail is readable, with its Plan

The portal SHALL offer a Run detail view reachable from the Runs table showing state,
timestamps, usage, output link and — when present — the Plan rendered as sanitised markdown,
with controls to approve or reject while the Run awaits approval.

#### Scenario: the reviewer reads the Plan where the decision is made

- **WHEN** a Member opens a Run awaiting approval
- **THEN** the Plan renders and both decisions are available; hostile markdown in the Plan is
  inert

### Requirement: a Run's file changes are reviewable beside its Plan

The Run detail view SHALL show the files the Run's change touched — path, status, added and
removed counts, and the unified patch rendered with added and removed lines visually
distinguished using design-system tokens. The read SHALL be live through the Connector at the
Run's linked change (BR-008). A Run with no pull request, a change touching no files, and a
failed read SHALL be three distinct messages. A file whose patch is omitted SHALL state the
reason and link to the vendor.

The changes SHALL occupy the detail's **main column**, never a side rail: a diff needs the width
the body has, with line numbers, and each file's header SHALL stay visible while its hunks scroll
and SHALL collapse on demand. The rail SHALL carry only the change's summary — its number, file
count and ± — anchoring to the block, so the two never show two diffs.

At a narrow viewport the diff SHALL wrap rather than scroll sideways, with the added/removed
marker in a fixed gutter so a wrapped line keeps its meaning; long paths SHALL truncate from the
left so the file name stays visible; files beyond the first SHALL arrive collapsed; and a hunk
longer than a screen SHALL paginate behind an explicit control naming how many lines remain.

#### Scenario: the reviewer sees what the Agent did

- **WHEN** a Member opens a Run whose pull request changed files
- **THEN** each file is listed with its status and counts, and its diff renders with added and
  removed lines distinguishable

#### Scenario: the diff has the width of the body

- **WHEN** the detail renders at a wide viewport
- **THEN** the changes occupy the main column with line numbers, each file's header stays visible
  while its hunks scroll, each file can collapse, and the rail shows only the summary that
  anchors to the block

#### Scenario: a phone reads the diff without sideways scroll

- **WHEN** the detail renders at a narrow viewport
- **THEN** diff lines wrap with the marker in a fixed gutter, paths truncate from the left, files
  beyond the first arrive collapsed, and long hunks offer "show more" instead of endless scroll

#### Scenario: no pull request yet

- **WHEN** the Run has produced no pull request
- **THEN** the section says so — distinctly from a change that touched no files

#### Scenario: an unshowable file is explained, not hidden

- **WHEN** a changed file is binary or its patch is too large
- **THEN** the file appears with a stated reason and a link to the vendor, and the other files
  still render

### Requirement: a Member cancels a Run, and nothing it started is published

The system SHALL let a Member cancel a Run that is not already terminal, ending it `Cancelled`
immediately (BR-012, DEC-041) — terminal, so the Story is freed (BR-001) and the record shows
the cancellation without inventing a failure reason (BR-014). The worker SHALL observe the
cancellation at its boundaries: a Run cancelled before its runtime is invoked SHALL not invoke
it, and a Run cancelled during an invocation SHALL publish nothing and SHALL NOT have its
cancellation overwritten by the outcome. Cancelling a terminal Run SHALL be refused with its
state named. Cancellation SHALL NOT terminate an Agent already running — that limitation is
documented, not implied.

#### Scenario: a queued Run is discarded

- **WHEN** a `Queued` or `AwaitingApproval` Run is cancelled
- **THEN** it ends `Cancelled`, nothing is enqueued or executed, and the Story can run again

#### Scenario: a Run cancelled mid-flight publishes nothing

- **WHEN** a Run is cancelled while its agent invocation is in progress
- **THEN** no commit, push or pull request happens, and the Run remains `Cancelled` after the
  invocation returns

#### Scenario: a terminal Run cannot be cancelled

- **WHEN** cancellation targets a `Succeeded`, `Failed` or `Cancelled` Run
- **THEN** it is refused with that state named, and nothing changes

### Requirement: a Run's cost is readable, and unknown is distinguishable from free

The runs API SHALL expose each Run's input tokens, output tokens and cost, nullable so that a
runtime which reported nothing yields null (BR-011, DEC-038). The portal SHALL render a
reported cost as an amount — including `0.00` for a free model — and an absent one as the
design system's empty value with the word unknown. The two SHALL NOT render alike. A project
SHALL show the summed cost of its Runs that reported, together with how many Runs are excluded
as unknown, so the total is never quietly understated.

#### Scenario: a Run that reported

- **WHEN** a Run's runtime reported usage
- **THEN** its cost and token counts are shown, and a zero cost is shown as zero

#### Scenario: a Run that did not report

- **WHEN** a Run's runtime reported no usage
- **THEN** its cost reads as unknown — not as zero

#### Scenario: the project total is honest about what it left out

- **WHEN** a project has Runs both with and without reported usage
- **THEN** the total sums only the reported ones and states how many were excluded

### Requirement: a Run can wait for a human's answer on its Story and resume

A Run SHALL be able to enter a waiting state when its agent pass ends with questions for a human.
The questions SHALL be posted as a comment on the Story carrying a marker identifying the Run,
and no container SHALL remain running while the Run waits. Waiting SHALL be untimed, exactly as
approval waits are, and a waiting Run SHALL still block its Story and SHALL still be cancellable.
A comment on the Story newer than the questions and not carrying the marker SHALL resume the Run
through the ordinary dispatch path, and the resumed pass SHALL receive the re-read Story together
with the conversation so far rather than any stored transcript.

#### Scenario: the pass ends with questions

- **WHEN** an agent pass ends by asking
- **THEN** the Run is waiting, its questions are on the Story with the run marker, and no
  container is running

#### Scenario: the human answers

- **WHEN** a comment without the marker arrives after the questions
- **THEN** the Run returns to the queue and its next pass sees the Story and every comment
  exchanged

#### Scenario: the agent's own comment

- **WHEN** the only comment after the questions carries the run marker
- **THEN** nothing resumes — whoever's account posted it

#### Scenario: waiting is untimed but not immortal

- **WHEN** a waiting Run is cancelled after hours without an answer
- **THEN** it is terminal, its Story is free, and a later comment resumes nothing

#### Scenario: waiting blocks the Story

- **WHEN** a Story has a waiting Run and a trigger would start another
- **THEN** the second is refused exactly as it would be for an executing Run

#### Scenario: unrelated Stories

- **WHEN** a comment arrives on a Story with no waiting Run
- **THEN** nothing is dispatched

### Requirement: everything waiting on a human is visible in one place

The product SHALL list every Run waiting on a human — a plan awaiting approval, a question
awaiting an answer, a failure awaiting a decision — across all projects, newest wait first, each
entry naming the project, the story, the reason and since when, and linking to the Run. A
`Failed` Run SHALL leave the list once a newer Run exists for its Story, because it no longer
waits on anyone. An ambient count of waiting work SHALL be visible from every portal page,
driven by the same data as the list. An empty inbox SHALL present as the good state it is.

A `Failed` Run SHALL also leave the list when a human has dismissed it, which is the decision that
no re-run is intended. Dismissal SHALL be recorded on the Run with the time it happened, because
nothing in the data distinguishes "nobody has decided yet" from "somebody decided not to act" — the
newer-Run condition stays derived, and this one is stored because a decision cannot be derived. A
dismissed Run SHALL remain `Failed`: dismissal records that somebody looked, and SHALL NOT change
what happened or cause anything to run.

Every count of failures awaiting a decision SHALL use the same condition as the list, so a count and
a list can never disagree.

#### Scenario: three kinds of waiting, two projects, one list

- **WHEN** Runs are awaiting approval, awaiting input and failed across two projects
- **THEN** the inbox lists them all with project, story, reason and age, newest first

#### Scenario: an entry leads to its Run

- **WHEN** an entry is followed
- **THEN** the Member lands on that Run with the relevant action available

#### Scenario: the count is ambient

- **WHEN** any portal page is open with three Runs waiting
- **THEN** the shell shows 3, and resolving one updates it

#### Scenario: resolution removes

- **WHEN** a waiting Run resumes, is approved, or reaches a terminal state that waits on nobody
- **THEN** it leaves the list and the count

#### Scenario: a re-triggered failure waits on nobody

- **WHEN** a Failed Run's Story has a newer Run
- **THEN** the failure no longer appears

#### Scenario: a dismissed failure waits on nobody

- **WHEN** a Member dismisses a Failed Run
- **THEN** it leaves the list and every count of failures awaiting a decision, and the Run is still
  `Failed`

#### Scenario: a dismissal is readable afterwards

- **WHEN** a dismissed Run is viewed
- **THEN** the dismissal and when it happened are visible

#### Scenario: only a failure can be dismissed

- **WHEN** dismissal is attempted on a Run that is not `Failed`
- **THEN** it is refused and nothing changes

#### Scenario: empty is good

- **WHEN** nothing waits
- **THEN** the inbox says so, as a good state rather than an error

### Requirement: a project's pulse is readable from data the Runs already carry

The system SHALL expose a project pulse over a seven-day window, computed at read time from
persisted Runs and the existing Contracts surfaces — no new storage. It SHALL report runs
started, success rate over terminal runs only, total cost stating how many runs were excluded
for unknown usage, mean queue wait and mean duration from the recorded timestamps,
per-automation fire and failure counts including automations with zero runs, stories never run,
the project-scoped waiting summary, and the age of the oldest unanswered question. On the
project page, every reported figure SHALL link to the list it summarises.

#### Scenario: figures a Member can verify by hand

- **WHEN** a project has runs across states and ages and its pulse is requested
- **THEN** only the window's runs are counted, the success rate covers terminal runs only, and
  every mean derives from the recorded timestamps

#### Scenario: an unused automation appears

- **WHEN** an automation fired no runs inside the window
- **THEN** the pulse lists it as unused rather than omitting it

#### Scenario: unknown cost is stated, not guessed

- **WHEN** some window runs carry unknown usage
- **THEN** the cost sums the known runs and states how many were excluded

#### Scenario: an empty project has a pulse

- **WHEN** a project has no runs at all
- **THEN** the pulse returns zeros and empty collections, not an error

### Requirement: a Run's output reaches watchers as it is recorded

While a Run executes, newly recorded output SHALL reach every open viewer of that Run in under
one second, without the viewer polling. Delivery SHALL be best-effort: when it is unavailable the
page SHALL fall back to the existing periodic read, and no output SHALL be lost either way,
because the durable record is unchanged. The agent's execution SHALL NOT depend on delivery in
any way — a Run behaves identically whether or not anybody is watching, and whether or not the
delivery path works.

The product's recorded decision about the latency budget SHALL state the same figures the
implementation uses.

A watcher joining a Run already in progress SHALL receive every line committed before it joined and
every line committed while it was joining. The subscription SHALL be established before the initial
read, so that lines arriving during the handshake are delivered rather than waiting for a later
reconciliation pass. Because that ordering produces an overlap between the pushes and the read, each
delivery SHALL carry the position it starts at and each read SHALL carry the position the next line
will occupy, so a viewer can discard what it already has. A delivery whose lines the viewer already
holds SHALL change nothing it displays.

The delivery mechanism SHALL NOT retain state for Runs that have reached a terminal state: a
terminal Run produces no further output, and its bookkeeping SHALL be released when its final output
is delivered. Concurrent deliveries for the same Run SHALL NOT produce a duplicated frame.

#### Scenario: a line appears while the Run executes

- **WHEN** the runtime emits a line and a viewer has the Run open
- **THEN** the line is rendered in under one second, without the viewer having requested it

#### Scenario: two viewers see one Run

- **WHEN** two viewers have the same Run open
- **THEN** both receive every line, and the work the portal does per line does not grow with the
  number of viewers

#### Scenario: delivery is unavailable

- **WHEN** the live path cannot be established or is lost
- **THEN** the page falls back to the periodic read and the full output remains available

#### Scenario: the Run does not care

- **WHEN** the live path is broken or nobody is watching
- **THEN** the Run executes and records its output exactly as it otherwise would

#### Scenario: a window opened mid-Run misses nothing

- **WHEN** a Member opens a Run's page while it is executing
- **THEN** every line committed before and during the subscription is visible within the stated lag
  budget, without waiting for a reconciliation pass

#### Scenario: an overlapping delivery is discarded, not appended

- **WHEN** a delivery carries lines the viewer's initial read already returned
- **THEN** those lines are discarded and nothing is shown twice

#### Scenario: a terminal Run leaves nothing behind

- **WHEN** a Run reaches a terminal state and its last output is delivered
- **THEN** the delivery mechanism retains no bookkeeping for it

#### Scenario: two deliveries for one Run

- **WHEN** two notifications for the same Run are handled concurrently
- **THEN** a watcher receives no duplicated frame

#### Scenario: the decision and the code agree

- **WHEN** the recorded latency decision is compared with the implementation's flush interval
- **THEN** they state the same figure

### Requirement: every Run reaches a terminal state, even when its worker never reports

A Run in a non-terminal executing state SHALL end whether or not the process executing it survives.
The system SHALL periodically end any Run in `Planning` or `Executing` whose **current phase's
start**, plus its Automation's timeout, plus a grace period, is in the past, marking it `Failed` with
a reason stating that it exceeded its timeout without its worker reporting.

The current phase's start SHALL be the moment that phase began, never a point before a human wait:
time spent `AwaitingApproval` or `AwaitingInput` SHALL NOT count toward any deadline (BR-006), and
each phase SHALL be measured against its own timeout (BR-005).

That reason SHALL be distinguishable from a timeout the executor enforced itself, because an agent
that was too slow and a worker that disappeared call for different responses.

Ending a Run this way SHALL NOT re-dispatch it and SHALL NOT create another (BR-004). It SHALL free
the Story (BR-001) and release the project's concurrency slot (BR-002), and the Run SHALL appear
wherever failures appear, so the occurrence is visible rather than silent.

The system SHALL NOT end a Run that is still within its deadline, and SHALL NOT overwrite a Run that
has reached a terminal state — a Run that finished between being observed and being written SHALL be
left exactly as it finished.

Overdue-ness SHALL be a property of the Run, not of the sweeping process: a Run that became overdue
while nothing was sweeping SHALL be ended on the next pass.

#### Scenario: a worker that vanished

- **WHEN** a Run's current phase has been running for longer than its Automation's timeout plus the
  grace period, and no worker has reported
- **THEN** it is `Failed`, its reason says its worker never reported, its Story accepts a new Run,
  and the project's concurrency count no longer includes it

#### Scenario: a long approval does not count against the work

- **WHEN** a Run planned, waited for approval for longer than its timeout, and has just begun
  executing
- **THEN** it is left untouched, because the phase it is in has only just started

#### Scenario: a slow Run inside its deadline

- **WHEN** a Run has been executing for less than its timeout, producing no output at all
- **THEN** it is left untouched

#### Scenario: a Run that finishes as the sweep runs

- **WHEN** a Run reaches a terminal state between being observed as overdue and being written
- **THEN** its outcome stands and the sweep changes nothing

#### Scenario: nothing is retried

- **WHEN** a Run is ended for exceeding its deadline
- **THEN** no Run is dispatched or created in its place

#### Scenario: overdue while nobody was watching

- **WHEN** the sweeping process is restarted after Runs have become overdue
- **THEN** those Runs are ended on the next pass

#### Scenario: the failure is visible

- **WHEN** a Run is ended for exceeding its deadline
- **THEN** it appears in the waiting inbox's failure lane like any other failure

### Requirement: a worker does not begin a phase it cannot finish

A worker whose remaining execution budget is less than one full phase timeout SHALL stop claiming
work and exit, leaving unclaimed messages for a worker started with a full budget. It SHALL NOT begin
a phase it knows it cannot complete.

This prevents the failure the sweeper recovers from. Recovery cannot be complete — a container may be
evicted at any moment — but a worker knowingly starting work it cannot finish is a choice.

#### Scenario: a worker near the end of its budget

- **WHEN** a worker's remaining budget is less than one phase timeout and the queue is not empty
- **THEN** it claims nothing further and exits, and the messages remain for the next worker

#### Scenario: a worker with budget to spare

- **WHEN** a worker's remaining budget exceeds one phase timeout
- **THEN** it claims and executes as normal

### Requirement: a Member re-runs a failure from where the failure is

A Member SHALL be able to re-run a `Failed` Run from that Run, without navigating elsewhere to find
the Story or choose an Automation. The new Run SHALL be created through the same path manual dispatch
already uses, with the failed Run's own Automation, so one active Run per Story, the project cap and
the approval gate all apply unchanged (BR-001, BR-002, BR-013).

Re-running SHALL NOT alter the failed Run, which remains the record of what happened. It leaves the
inbox because a newer Run exists, which is the condition already in force.

A re-run refused because the Story already holds an active Run SHALL be reported in the same terms
manual dispatch uses for the same refusal.

#### Scenario: re-running from the failure

- **WHEN** a Member re-runs a Failed Run
- **THEN** a new Run exists for the same Story with the same Automation, the failed Run is unchanged,
  and the failure leaves the inbox

#### Scenario: the Story is already busy

- **WHEN** a Member re-runs a Failed Run whose Story holds an active Run
- **THEN** the attempt is refused naming one active Run per Story, in the same terms manual dispatch
  uses

#### Scenario: the gate still applies

- **WHEN** the failed Run's Automation requires approval
- **THEN** the new Run waits for a plan to be approved exactly as manual dispatch would produce

### Requirement: a Run's output reads as a transcript, not as raw events

A Run's output SHALL be presented as a transcript a person can read without parsing JSON. The agent's
own text SHALL be shown as prose through the same sanitising pipeline a Story description uses, and no
internal identifier — session, message or part — SHALL appear inside it. Each tool the agent invoked
SHALL appear as one compact line naming the tool and its subject, with the full detail one disclosure
away.

Interpretation SHALL be dialect-tolerant rather than dialect-specific: a line SHALL be treated as a JSON
object if it parses and as text if it does not, well-known fields SHALL be lifted when they are present,
and the remainder SHALL be pretty-printed. The presentation SHALL NOT branch on which runtime produced
a line, so that adding a runtime requires no change here.

A line the portal cannot interpret SHALL be shown verbatim and SHALL NOT prevent the rest from
rendering. Completeness SHALL take precedence over presentation: nothing recognised is nothing lost.

While a Run executes, a running total of tokens and cost SHALL be shown from what the lines carry, and
SHALL update as lines arrive. Absent usage SHALL read as unknown, never as zero. The Run's own recorded
usage SHALL remain authoritative once the Run has ended.

The stored transcript SHALL be unchanged by any of this: each persisted chunk SHALL still hold the exact
line the process emitted, with no schema change and no normalised event store.

The output area SHALL render its documented pattern in each of its four states — empty, loading, failed
and populated — in both themes, and SHALL be reachable by keyboard.

#### Scenario: the agent's words are prose

- **WHEN** a Run's output contains assistant text inside an event envelope
- **THEN** the text is rendered as sanitised prose, with no session, message or part identifier shown
  in it

#### Scenario: a tool invocation is one line

- **WHEN** the agent invoked a tool
- **THEN** the transcript shows one compact line naming the tool and its subject

#### Scenario: an unknown dialect degrades rather than disappears

- **WHEN** a line is a JSON object whose text field the portal does not recognise
- **THEN** the object is pretty-printed and remains fully readable

#### Scenario: a line that is not JSON at all

- **WHEN** a plain stderr line or a malformed event is rendered
- **THEN** it is shown verbatim and the rest of the transcript still renders

#### Scenario: spend while spending

- **WHEN** a Run is executing and its lines carry token counts
- **THEN** a running total of tokens and cost is shown and grows as lines arrive

#### Scenario: no usage is unknown, not free

- **WHEN** a Run's lines carry no usage at all
- **THEN** the counter reads unknown rather than zero

#### Scenario: the stored line is untouched

- **WHEN** a Run's log chunks are read from the database after any of this rendering
- **THEN** each chunk still holds exactly the line the process emitted

### Requirement: a Member holds a conversation with an agent without occupying a Run

A Member SHALL be able to hold a conversation with an agent from the portal. A conversation SHALL
belong to a project and MAY name one of that project's Stories as its subject; naming none SHALL be
an ordinary case and not a degraded one.

A conversation SHALL NOT be a Run. It SHALL occupy no cap slot, hold no lock on any Story, and block
nothing: an Automation whose trigger is applied to a Story with an open conversation SHALL match and
run exactly as it would otherwise. This is what keeps BR-001 and BR-014 untouched — waiting blocks a
Story because a Run occupies it, and a conversation that occupied one would stop every Automation on
that Story for as long as somebody kept talking.

Holding a conversation SHALL require a permission the Member bundle holds, scoped to the project, so
a caller with no role on a project cannot open one about it.

#### Scenario: a conversation exists without a Run

- **WHEN** a Member starts a conversation on a project
- **THEN** it exists with no Run created, optionally naming a Story and otherwise naming none

#### Scenario: an open conversation blocks nothing

- **WHEN** a trigger label is applied to a Story that has an open conversation
- **THEN** the Automation matches and its Run starts as usual

#### Scenario: a conversation belongs to a project a caller may see

- **WHEN** a caller with no role on a project tries to start or read a conversation about it
- **THEN** it is refused for permission, disclosing nothing about the project

### Requirement: a message costs exactly one agent pass, and the spend is visible

Sending a message SHALL run exactly one agent pass. The reply SHALL be readable in the portal, and
the pass's usage and cost SHALL be recorded against the conversation.

A pass whose usage the runtime did not report SHALL read **unknown**, never zero (BR-011). A
conversation whose total includes an unmeasured pass SHALL NOT present that total as exact. This
holds for an attempt at a prompt as for any other message: money that was spent SHALL be recorded on
the row that spent it, and SHALL NOT vanish because the text was a draft.

The agent SHALL be given the project's repository, cloned with the project's credential, so answers
are grounded in the code and not only in the mirror. Where a conversation names a Story, the agent
SHALL additionally be given that Story's context read from the mirror, as any other read is (BR-008).
A conversation naming no Story SHALL cause no vendor write on any Story.

**A Story SHALL be described to an agent in one way, whichever path supplies it.** The description
SHALL carry the Story's number, title, state, labels and description, and the description SHALL be
bounded, so that a prompt tried in a conversation and then run by an Automation is given the same
input rather than two framings that differ in what a prompt may branch on.

A message SHALL be bounded, and the bound SHALL admit the prompts this product exists to author: it
SHALL be large enough that a real prompt file is accepted rather than refused as a validation error
on somebody's paste.

A conversation SHALL leave no comment or other trace on the Story at the vendor.

#### Scenario: one message, one pass

- **WHEN** a Member sends a message
- **THEN** exactly one agent pass runs and its reply is readable

#### Scenario: what a pass cost is recorded against the conversation

- **WHEN** a pass reports its usage
- **THEN** the conversation's spend includes it and is readable

#### Scenario: unmeasured is not free

- **WHEN** a pass reports no usage
- **THEN** that message reads unknown, and the conversation's total does not claim to be exact

#### Scenario: a conversation about nothing writes nothing

- **WHEN** a conversation naming no Story exchanges messages
- **THEN** no vendor write happens on any Story

#### Scenario: a conversation about a Story still writes nothing

- **WHEN** a conversation naming a Story exchanges messages
- **THEN** the Story at the vendor is unchanged, and the agent was given that Story's context

#### Scenario: both paths describe a Story identically

- **WHEN** the same Story is supplied to an agent by a Run and by a conversation
- **THEN** the description is the same in both, carrying its number, state and labels, and bounded in
  both

#### Scenario: a real prompt is not refused for its length

- **WHEN** a message the length of a real prompt file is sent
- **THEN** it is accepted, and a message beyond the bound is still refused

### Requirement: a failed message leaves the conversation open

A pass that fails SHALL be shown as a failure on the message that caused it, and the conversation
SHALL stay open. A failed message SHALL NOT be a failed conversation, and SHALL NOT prevent the next
message.

#### Scenario: a failure is a message, not an ending

- **WHEN** an agent pass fails
- **THEN** the conversation shows the failure, stays open, and accepts another message

### Requirement: a conversation's agent runs where the habitat provides one

The agent pass SHALL run behind a runtime seam, so the module that owns conversations does not know
where the agent executes.

Where the habitat provides an on-demand session host, a conversation SHALL get **its own** container
for as long as it is being used, carrying the cloned workspace and the project's credential, and the
host SHALL reclaim it after inactivity. One conversation SHALL map to one container, so isolation
coincides with the credential boundary (DEC-030) and no project's credential is visible to another.

Where the habitat provides none — a machine one person owns, or a deployment with no session host —
the pass SHALL run in process, and the conversation SHALL behave identically. Which habitat this is
SHALL be decided by configuration and never inferred (ADR-0010).

#### Scenario: a session belongs to one conversation

- **WHEN** two conversations on different projects are active at once
- **THEN** each runs in its own container with only its own project's credential

#### Scenario: no session host is not a broken conversation

- **WHEN** the habitat provides no session host
- **THEN** conversations work, with the pass running in process

### Requirement: an Admin tries a prompt before committing it

An Admin SHALL be able to supply prompt text in the portal and run it once against the project's
repository, reading the reply and what it cost, without committing a file, configuring an Automation,
or applying a trigger label.

Each attempt SHALL be an independent trial: an edited draft SHALL reach an agent that has not seen
the previous draft or its reply, so the result of trying predicts the result of running rather than
continuing a discussion about the earlier attempt.

The text supplied SHALL NOT be stored anywhere the Run path reads. No Automation SHALL be written or
changed by an attempt, and a Run executing afterwards SHALL resolve its prompt from the repository
exactly as it would have otherwise. The surface SHALL say plainly that the text is not saved, and
SHALL name where it belongs when it is right — the project's prompts directory.

Trying a prompt SHALL require the same permission as holding a conversation, scoped to the project.
A caller with no role on the project SHALL be refused, disclosing nothing about it, and a Member
SHALL be allowed: an attempt spends exactly what a conversation message spends, and gating it more
tightly would be an inconsistency with no argument behind it.

An attempt SHALL NOT be a Run. It SHALL create no Run, occupy no cap slot and lock no Story, and an
Automation whose trigger is applied to a Story with an attempt in flight SHALL match and run as
usual.

#### Scenario: a prompt is tried without being committed

- **WHEN** an Admin supplies prompt text for a project with a Connector and runs it
- **THEN** exactly one agent pass runs against that project's repository and the reply is readable

#### Scenario: an edited draft is tried afresh

- **WHEN** an Admin edits the text and runs it again
- **THEN** the agent is given the edited text without the previous attempt or its reply

#### Scenario: trying changes no configuration

- **WHEN** an attempt has run
- **THEN** no Automation has been written or changed, and a Run afterwards resolves its prompt from
  the repository

#### Scenario: an attempt occupies nothing

- **WHEN** a trigger label is applied to a Story that has an attempt in flight
- **THEN** the Automation matches and its Run starts as usual

#### Scenario: trying is refused to a caller with no role

- **WHEN** a caller with no role on a project tries a prompt against it
- **THEN** it is refused for permission, disclosing nothing about the project

### Requirement: a trial is faithful to a Run, and says where it is not

The text an Admin supplies SHALL reach the agent framed as a Run frames the repository's prompt: the
prompt text, then the named Story's description in the same form a Run uses.

Where a trial cannot reproduce a Run, the surface SHALL name the difference rather than leave it to
be discovered from a divergent result. The differences SHALL be: an approval-gated Automation runs
its prompt in a planning phase whose framing a trial does not reproduce, and a per-Automation timeout
belongs to an Automation a trial does not have.

#### Scenario: a trial naming a Story is framed as a Run would frame it

- **WHEN** an attempt names a Story
- **THEN** the agent receives the prompt text followed by that Story's description in the same form a
  Run supplies

#### Scenario: naming no Story is ordinary

- **WHEN** an attempt names no Story
- **THEN** the pass runs against the project alone and is not treated as incomplete

### Requirement: a Run records where it executes, chosen at creation

Every Run SHALL carry an execution locus — `Pod` or `Local` — fixed at creation and exposed on
the runs read model. `Run now` MAY name a locus explicitly; when absent, and always for
matching-created Runs, the project default applies: `Local` for a LocalFolder code source, `Pod`
otherwise. A `Local` locus on a project whose code source is not a folder SHALL be refused, as
SHALL `Pod` on a LocalFolder project — an Agent pod cannot see the host's disk. BR-001, BR-002
and BR-013 apply identically to both loci.

#### Scenario: the default follows the code source

- **WHEN** matching creates a Run for a project whose Connector's code source is a local folder
- **THEN** the Run records `locus=Local` and the read model exposes it

#### Scenario: an impossible locus is refused

- **WHEN** Run now names `pod` for a LocalFolder project
- **THEN** creation is refused with a sentence naming the constraint, and no Run exists

### Requirement: a Local dispatch requires a clean working tree (BR-016)

Dispatching a Run with locus `Local` SHALL verify the configured folder's working tree is clean
and refuse before any write when it is not — the refusal names the folder and says why, in the
same pre-write pattern as BR-001. The rule is recorded as BR-016 in
`docs/product/mvp/05-business-rules.md` as part of this change.

#### Scenario: dirty tree refuses the dispatch

- **WHEN** Run now targets a Local project whose folder has uncommitted changes
- **THEN** the answer is a refusal naming the folder, and no Run was created

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

### Requirement: a failure arrives with its remedy and its decisions

A failed Run's detail SHALL open with a banner above the content carrying the full failure
reason, and the failure's two decisions — re-run and dismiss (#145) — SHALL live inside that
banner and nowhere else on the page: the decisions belong beside the reason they answer, not in a
header far from the why.

A cause with a known remedy surface SHALL link to it from the banner: an unresolved secret or
credential links to the project's Connector settings, and an unreadable prompt file links to the
Automations tab. A cause with no mapped surface SHALL show the banner without a link — a guessed
remedy is worse than none. The mapping is a stated, short list, never an inference from message
text beyond the causes it names.

A Run section with nothing to show — a Plan that never existed, output that never arrived — SHALL
collapse to a single line stating its empty state rather than holding blank space.

#### Scenario: the failure is answerable where it is stated

- **WHEN** a Member opens a failed Run
- **THEN** the banner shows the full reason with re-run and dismiss inside it, and those controls
  appear nowhere else on the page

#### Scenario: a mapped cause links its remedy

- **WHEN** the failure is an unresolved secret or credential
- **THEN** the banner links to the project's Connector settings

#### Scenario: an unmapped cause gets no invented link

- **WHEN** the failure matches no mapped cause
- **THEN** the banner shows the reason and the decisions, and no remedy link

#### Scenario: empty sections take one line

- **WHEN** a Run has no Plan or produced no output
- **THEN** each such section renders as a single line stating so

