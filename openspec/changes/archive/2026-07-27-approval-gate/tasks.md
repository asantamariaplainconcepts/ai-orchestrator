# Tasks — approval-gate

## 1. The record

- [x] 1.1 `Plan` (nullable, bounded) + `ApprovedAt` on Run; `Cancelled` state; migration.
      BR-001's index filter keeps its active-state list — `Cancelled` is outside it (design D5).
      **Found while testing:** the creation pre-check had a hand-copied state list that never
      learned about `Cancelled`, so a rejected Run kept holding its Story — the second time
      that copy drifted from the index. Both now derive from one `RunStates.Active` array,
      which also generates the index's SQL filter.
- [x] 1.2 `RunCreator` stops refusing the two-phase lane: `TwoPhaseRefused` and its error go,
      and an approval-gated match creates a Run like any other.

## 2. The phases

- [x] 2.1 Executor routes on the record (design D1): approval-gated + no `ApprovedAt` → plan
      phase (prepare, plan instruction, store Plan, `AwaitingApproval`, publish nothing);
      otherwise → execution, with the approved Plan in the instruction when there is one (D2).
- [x] 2.2 Approve/reject slices: `POST .../runs/{runId}/approve` (stamp, `Queued`, re-enqueue)
      and `POST .../runs/{runId}/reject` (`Cancelled`). Both refuse a Run that is not awaiting
      approval, with a distinct answer.
- [x] 2.3 `ListRuns` and the Run read expose the Plan and `ApprovedAt`.

## 3. Tests

- [x] 3.1 The pause: Plan stored, `AwaitingApproval`, workspace never published.
- [x] 3.2 Approve → re-enqueued, executes, PR link, and **the instruction contains the Plan**.
- [x] 3.3 Reject → `Cancelled`, nothing enqueued, the Story runs again.
- [x] 3.4 BR-006/BR-002/BR-001 while awaiting: untimed, no cap slot, still holds the Story.
- [x] 3.5 Approve/reject on a Run in the wrong state are refused distinctly.

## 4. The portal

- [x] 4.1 Run detail route: state, timestamps, usage, output, Plan as sanitised markdown
      (design D6), Approve/Reject; the Runs table links to it; catalog copy; lint + build.

## 5. Close-out

- [x] 5.1 ARCHITECTURE.md: the two-phase lane replaces its stated limitation; full suite;
      CI green.
