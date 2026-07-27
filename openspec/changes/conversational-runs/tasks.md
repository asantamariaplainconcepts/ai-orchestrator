# Tasks — conversational-runs

## 1. Domain and schema

- [x] 1.1 `RunState.AwaitingInput` + `AwaitInput(at)`/`Resume(at)` on Run, shaped exactly like
      the approval pair; `WaitingSince` column.
- [x] 1.2 `RunStates.Active` gains the state; migration regenerates the BR-001 partial index
      (design D5).

## 2. The seam and contracts

- [x] 2.1 `ReadComments(coordinates, vendorStoryId, since, token)` on `IBacklogConnector`;
      GitHub implementation; Azure DevOps implementation (unexercised, like its siblings).
- [x] 2.2 Contracts surface (`IConversationReader`) so the Runs module reads without touching
      the Backlog implementation.

## 3. Await and resume

- [x] 3.1 Executor primitive: end a pass with questions → marker comment + `AwaitInput`
      (unreachable until #79, stated in ARCHITECTURE.md per ADR-0006 — the consumer is the next
      issue in the chain).
- [x] 3.2 Resume check on the polling cadence: unmarked comment newer than the questions →
      `Resume` → ordinary dispatch. The run marker constant lives in one place.

## 4. Tests

- [x] 4.1 Functional: questions → waiting + marker comment; answer → requeued with conversation;
      marker-only comment resumes nothing; cancel while waiting frees the Story and later
      comments do nothing; BR-001 holds against a waiting Run; unrelated Story comments are
      inert.
- [x] 4.2 Unit: the state machine additions; marker parsing.

## 5. Close-out

- [x] 5.1 BR-006's text grows from approval waits to human waits; ARCHITECTURE.md documents the
      wait/resume machinery and its unconsumed status; CI's filtered command locally; CI green.
