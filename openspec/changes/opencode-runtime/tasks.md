# Tasks — opencode-runtime

## 1. Selection

- [x] 1.1 `IAgentRuntimeSelector` in BuildingBlocks (runtime + optional credential name);
      composition registers Claude Code (credential `anthropic-api-key`) and OpenCode (none by
      default); executor asks the selector (design D1/D3) — no conditional on runtime names in
      the executor.
- [x] 1.2 `AgentRuntime.OpenCode` in Projects; frontend runtime list + catalog hint.

## 2. The implementation

- [x] 2.1 `OpenCodeRuntime`: pinned CLI, `--format json`, usage summed from step_finish, text
      as log, D4's honesty split. Config: `Runs:OpenCode:Model` default
      `opencode/deepseek-v4-flash-free`.
- [x] 2.2 Unit tests at the parser level (event fixtures from the spike) + the timeout path via
      the command seam, as the Claude Code runtime did.

## 3. Exercise

- [x] 3.1 Image pins opencode beside claude; in-container spike: the free-model run in a clean
      environment — record whether no-credential survives (design D2's unverified half),
      either way stated in D2. **Outcome: VERIFIED** — clean container (HOME=/tmp, no ambient
      state), free model answered with exit 0, same event shapes, cost 0, no credential.
- [x] 3.2 Functional: an OpenCode-runtime Automation reaches its (faked) runtime and a
      ClaudeCodeHeadless one reaches the existing path; free-model path performs no vault
      lookup.

## 4. Close-out

- [x] 4.1 OPN-004 closure in docs/product/mvp/07; ARCHITECTURE.md runtime-selection sentence;
      guardrails + full suite; CI green.
