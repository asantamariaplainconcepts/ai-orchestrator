# runtime-readiness — proposal

Issue: #279 · Product · Actors: ACT-001/ACT-002 (whoever watches a Run fail), the dev-loop
developer · UC-012 and the runs observability surfaces · BR-004, BR-010

## Why

An agent runtime's absence is discovered only by a Run dying with the raw process error
(`An error occurred trying to start process 'opencode' … No such file or directory`), and a
missing credential fails naming the secret but not where to put it. Observed on a real machine
(2026-08-07): both desktop apps logged in, neither CLI on PATH, two automations failing mute —
the same silence the pods panel already ended for docker (#254).

## What Changes

- **The environment panel covers agent runtimes like it covers pods**: a probe per registered
  runtime on a stated cadence — the CLI answers `--version`, the configured credential resolves
  — feeding the existing panel pattern: state chip, last-checked time, copyable remedy command,
  i18n copy as contract with the guides.
- **A Run that still fails carries the remedy in its failureReason** (BR-004: nothing retries,
  so the failure must carry everything): executable missing → the binary, that PATH resolution
  failed, and the install command; secret missing → the store and how to add the secret (never
  a value, BR-010).
- **The Claude credential requirement becomes switch-off-able**: `ClaudeCodeHeadless`
  hard-defaults `CredentialSecretName` to `anthropic-api-key` and, unlike opencode's, never
  normalizes empty→null — a machine whose `claude` CLI is session-authenticated cannot run it
  at all. Empty/whitespace configuration means "no secret; the machine's own session", exactly
  as opencode already behaves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `agent-execution`: gains a requirement — the agent runtimes are observable where they run
  (probe, panel, remedy-carrying failures, and the credential switch-off's session semantics).
  The runtime-seam requirement itself is untouched: resolving credentials **by name** already
  admits "no name configured".

## Impact

- `src/shared/AiOrchestrator.ServiceDefaults/Agents/` — runtime probe (mirrors
  `AgentPodsProbe`), credential normalization in `AddAgentRuntime`, remedy wording in the
  process-start failure path.
- `src/modules/Runs/` — the executor's failure sentences; the panel read joined with runtime
  states (or a sibling of `GET /api/pods`).
- `src/frontend/features/pods/` (or sibling) + `shared/i18n/en.ts` — panel copy, remedy
  commands as copy-is-contract.
- Tests: DispatchTests/agent-runtime tests for selection + normalization; functional tests for
  the read; E2E for the panel rendering a not-ready runtime.
