# Proposal: opencode-runtime

## Why

Issue #30 (Foundation, DEC-012's second runtime). One implementation proves a seam compiles;
a second proves it is a seam. And the owner's practical driver: opencode ships free models
(`opencode/*-free`), observed running headless with no credential — the whole agent path
becomes exercisable at zero model cost. OPN-004 closed during the grill against the real CLI
(v1.18.6): JSONL event stream, usage in `step_finish` events, reply in `text` events.

## What Changes

- **Runtime selection by Automation**: the executor asks an `IAgentRuntimeSelector` for the
  runtime (and its credential secret name) matching the Automation's `Runtime` — selection is
  composition, not a conditional. `AgentRuntime` gains `OpenCode`; the portal's runtime field
  gains the value.
- **`OpenCodeRuntime`** beside the Claude Code implementation: pinned CLI, headless
  `--format json`, usage summed from `step_finish` tokens/cost, text events as the log,
  defensive on unknown shapes (BR-011's unknown, never invented numbers).
- **Per-runtime credentials, absence allowed**: each runtime names its secret via config;
  opencode's default is none (free model), Claude Code keeps `anthropic-api-key`. Default
  model `opencode/deepseek-v4-flash-free` (config-overridable).
- **Image**: the worker image pins opencode beside claude; the in-container spike repeats the
  free-model run in a clean environment and records the outcome.
- **OPN-004 closure** lands in docs/product/mvp/07-open-decisions.md.

## Impact

- Affected specs: `agent-execution` (runtime selection requirement).
- Touched: BuildingBlocks (selector seam), ServiceDefaults (opencode impl + composition),
  Runs module (executor asks the selector), Projects module (enum value), frontend (runtime
  list), Dockerfile, docs/product/mvp/07, tests.
- Out of scope: model picker UI, provider management, ceremony changes.
