# Proposal: sync-action

## Why

Issue #123 (ACT-002 triggers, ACT-001 configures). The product runs three quarters of the
workflow it was built for: grill interrogates, propose writes the spec, implement opens the pull
request. The fourth — closing the change — is still a terminal command. It is the step that
happens most often and the only one an agent cannot reach.

Closing a change is also where a project's conventions live most heavily, which is why it must
not be a procedure this product knows. The grill already solved that shape: it reads the
connected repository's own readiness document rather than imposing a bar (DEC-048). Sync reads
the repository's own close-out process the same way.

## What changes

- **A seventh action, `SyncChange`** (DEC-048's licensed growth), executed on the implement
  pipeline's shape: workspace, agent, outcome, cost. Not a new kind of thing.
- **The procedure comes from the repository** (design D1), at a configurable path defaulting to
  the framework's convention — exactly as the grill's rubric does, including its refusal when the
  document is absent.
- **It closes a change that exists** (design D2): the Story's open change, found through the same
  workspace seam propose already uses to enforce one-change-per-Story. No open change is a
  refusal, not an improvisation.
- **Approval-gated in the seeded defaults** (design D3): merging is the most irreversible thing
  this product would do, and DEC-040 exists for that.
- **Every refusal precedes the workspace** (design D4), as propose established: no spend on a
  Story this action cannot honestly serve.

## Impact

- Specs: `agent-execution` (one ADDED requirement) and `automation-configuration` (one MODIFIED —
  the defaults gain a seventh entry).
- Code: the action in the executor's dispatch, its prompt, its refusals; one more entry in the
  catalogue and in the defaults.
- No schema change: the action is a value of the existing enum, and the document path reuses
  `RubricPath`'s shape.

## Out of scope

Deciding *whether* to merge — that judgement is the approval gate's, not the agent's. Release or
deployment steps after the merge. Any repository-specific procedure hardcoded here.
