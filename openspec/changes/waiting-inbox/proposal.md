# Proposal: waiting-inbox

## Why

Issue #94 (UC-026). Three Run states wait on a human — `AwaitingApproval` (DEC-040),
`AwaitingInput` (#78), `Failed` (BR-004) — and all three live scattered across per-project
pages. The conversational actions made this acute: a Run that asked a question waits untimed
(BR-006) on a page nobody is looking at, and its Story stays blocked (BR-001) the whole time.
Borrowed shape: Orbion's triage inbox — the strongest presence idea in the sibling product.

## What Changes

- **`GET /api/inbox`** — every Run waiting on a human, across all projects, newest wait first.
  Each entry carries the project, the story, *what it waits for* (a plan to approve, a question
  to answer, a failure to decide about) and since when.
- **A `Failed` Run leaves the inbox when a newer Run exists for its Story** — it waits on
  nobody; the human already acted. Without this the inbox accumulates corpses and people stop
  reading it, which is how inboxes die.
- **An inbox page** linking each entry to its Run, and **an ambient count in the shell** driven
  by the same endpoint. The count is the website being the surface: DEC-037 is not reopened.

## Impact

- Affected specs: `run-orchestration` (the waiting surface).
- Touched: Runs module (one read slice — cross-project is fine, the module owns all Runs),
  Backlog contracts (story titles via the existing reader), frontend (page + shell badge),
  tests, UC-026 into the corpus.
- Out of scope: notifications beyond the portal (would revise DEC-037 — its own issue); acting
  inline from the inbox (v1 links to the Run); Stories or Automations waiting — this is Runs.
