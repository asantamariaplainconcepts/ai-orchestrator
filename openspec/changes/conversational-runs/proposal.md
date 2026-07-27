# Proposal: conversational-runs

## Why

Issue #78 (Foundation). Every Run is one shot: dispatch, act, terminate. An action that needs to
*ask something* — the grill action is the first, #79 — has nowhere to wait for the answer. The
approval gate already proved the shape this product wants for human waits: a Run whose container
has exited, waiting untimed (BR-006), resumed by the human's act. This generalises that machinery
from "approve/reject" to "answer on the Story", and deliberately ships no action that uses it —
that is #79's job, and RULE-005 forbids smuggling the enabler inside the feature.

## What Changes

- **`RunState.AwaitingInput`** — entered when an agent pass ends with questions. Active for
  BR-001 (a waiting Run blocks its Story), untimed like approval (BR-006 grows from "approval
  waits" to "human waits"), cancellable like anything else (UC-014).
- **The questions are a Story comment carrying a run marker** (`<!-- aio:run:<id> -->`). One
  project PAT (DEC-030) means agent and human can be the same vendor account, so authorship
  cannot tell question from answer; the marker can.
- **`ReadComments` on the Connector seam** and a Contracts surface for it — comments are never
  mirrored (BR-008), so they are read live at resume time.
- **A resume check** over waiting Runs: a comment newer than the agent's questions, without the
  marker, sends the Run back to `Queued` — the same move `Approve` makes — and ordinary dispatch
  does the rest. The resumed pass is stateless: it re-reads the Story and the whole conversation.

## Impact

- Affected specs: `run-orchestration` (the waiting state and resume), `connector-seam` (one read).
- Touched: Runs module (state, migration — the BR-001 partial index filter names the active
  states, so it changes), Backlog module (seam + both vendors + Contracts), the executor's
  await/resume primitives, tests, ARCHITECTURE.md, BR-006's text.
- Out of scope: any action using the wait (#79); webhook-driven resume (the periodic check
  suffices; a webhook is a latency optimisation later); mirroring comments; UI beyond the state
  showing in the existing runs list.
