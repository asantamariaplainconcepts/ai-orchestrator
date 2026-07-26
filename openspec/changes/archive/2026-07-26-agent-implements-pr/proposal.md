# Proposal: agent-implements-pr

## Why

Issue #19 (UC-016) — the loop's payload. #18 proved the contract with a deterministic
instruction; nothing yet clones code, implements, or opens a PR. This change makes the
`ImplementToPullRequest` action real: the first time the product's whole promise — label a
Story, get a PR — executes end to end.

## What Changes

- **`ICodeWorkspace` in BuildingBlocks** — the ceremony as a seam: `Prepare` (clone with the
  PAT, branch `run/<id>`) and `Publish` (commit, push, open the PR, return its URL). The
  git/Octokit implementation lives in ServiceDefaults; the functional tier substitutes it like
  every other seam. The Agent's job is the implementation; the ceremony is deterministic code.
- **The executor orchestrates stages**: prepare → agent → publish, each failure carrying its
  stage's distinct reason (clone vs agent vs no-changes vs push/PR). An Automation action
  other than `ImplementToPullRequest` fails stating the action is not executable yet.
- **Run gains `OutputLink`** (migration); ListRuns exposes it; the Runs table's Output column
  replaces its em-dash with the PR link when present.
- **BR-005 becomes observed behaviour**: the timeout pass-through from #18 is asserted — a
  runtime exceeding the Automation's timeout ends the Run `Failed` naming the limit.

## Impact

- Affected specs: `agent-execution` (extends execution with the implement→PR requirement).
- Touched: BuildingBlocks (workspace seam), ServiceDefaults (git/Octokit implementation),
  Runs module (executor stages, OutputLink, migration), ListRuns + frontend Output column,
  Runs functional tests (fake workspace), worker image (git already present).
- Out of scope: plan phase, story-side PR echo, other actions, retries.
