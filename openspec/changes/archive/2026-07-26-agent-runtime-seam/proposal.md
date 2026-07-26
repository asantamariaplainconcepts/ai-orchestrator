# Proposal: agent-runtime-seam

## Why

Issue #18 (Foundation — "Runtime seam", docs/product/mvp/09). A dispatched Run currently
vanishes into a worker that logs and exits. The job contract — story prompt in; plan/output/
usage out — must be designed once at a real seam (DEC-012: Claude Code headless first,
opencode second), and ADR-0001 demands it be exercised inside the job template, not on paper.
Exercising it requires Runs that can end, so terminal states and the BR-001 index revision
land here too.

## What Changes

- **`IAgentRuntime` in BuildingBlocks**: instruction in (prompt, workspace, action, timeout,
  in-memory credentials), result out (output link?, log, usage?). No CLI or vendor type
  crosses it — the ISecretResolver placement rule.
- **`ClaudeCodeHeadlessRuntime`** beside the other infrastructure implementations: invokes the
  pinned CLI in headless JSON mode, parses the result defensively — any usage parse miss is
  null, and null is "unknown" on the Run, never a failure (BR-011/DEC-038).
- **Run lifecycle**: `Succeeded`/`Failed` terminal states; Queued → Executing at claim;
  timestamps per BR-014. The BR-001 partial index keeps its filter list — terminal states now
  exist and are excluded, so a finished Story can run again.
- **The worker becomes a host like the Server**: composes modules, claims a Run id, loads
  Run/Story/Automation through the module surfaces, resolves the PAT and AI credential by
  name at execution time (BR-010/DEC-014/DEC-030 — nothing travels in the message), invokes
  the runtime, records the outcome.
- **Image**: the existing worker Dockerfile gains node + the pinned Claude Code CLI;
  deploy.sh already builds and pushes it. No second pipeline.

## Impact

- Affected specs: new capability `agent-execution`.
- Touched: BuildingBlocks (seam), ServiceDefaults/worker (runtime + lifecycle), Runs module
  (states + migration), Dockerfile.dispatch, functional tests (a fake runtime at the seam),
  spike artifacts in design D2.
- Out of scope: implement→PR (#19), plan phase, opencode (OPN-004), cancellation, BR-005
  enforcement beyond passing the timeout through.
