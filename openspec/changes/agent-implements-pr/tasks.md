# Tasks — agent-implements-pr

## 1. The workspace seam

- [ ] 1.1 `ICodeWorkspace` in BuildingBlocks: `Prepare` → workspace on branch `run/<id>`;
      `Publish` → PR URL; stage-named refusals as data (design D1/D4).
- [ ] 1.2 Git+Octokit implementation in ServiceDefaults (design D2): in-memory credential URL
      per invocation, no persisted remote config; `git status --porcelain` is the no-changes
      gate (D3). Composition beside AddAgentRuntime.

## 2. The executor's stages

- [ ] 2.1 RunExecutor: action gate (only ImplementToPullRequest executes; others fail stating
      so), prepare → agent → publish, stage-distinct failure reasons, OutputLink recorded on
      success.
- [ ] 2.2 Run.OutputLink + migration; ListRuns exposes it (exact-shape test updated
      deliberately); the Output column renders the link.

## 3. Tests

- [ ] 3.1 Functional, fake workspace + fake runtime: label→PR happy path (URL on the Run and
      in the API), no-changes failure, per-stage failure reasons, non-executable action, and
      the BR-005 timeout path against the real CLI runtime with a sleeping fake command — or,
      where that cannot be expressed honestly, the timeout unit-tested at the runtime level
      and stated.

## 4. Close-out

- [ ] 4.1 Guardrails green; ARCHITECTURE.md updated; frontend lint/build; full suite; CI
      green.
