# Tasks — agent-runtime-seam

## 0. The spike (ADR-0005 — design D2 is a hypothesis until this runs)

- [x] 0.1 Build the job image with the pinned CLI; run it headless with a trivial prompt and
      an operator-supplied credential; record the observed result JSON (usage/cost shape) in
      design D2. If the operator credential is unavailable in-session, state so in D2 and
      verify at first deployed run — the defensive parser makes either order safe.
      **Outcome: half-proven.** The image builds and `claude --version` answers `2.0.44
      (Claude Code)` inside the container. The JSON result shape remains the D2 hypothesis —
      no operator credential was available in-session; first deployed run verifies it, and the
      defensive parser bounds the damage of a wrong guess to "usage reads unknown".

## 1. The seam and lifecycle

- [x] 1.1 `IAgentRuntime` + instruction/result records in BuildingBlocks; no vendor type in
      any signature.
- [x] 1.2 Run gains `Succeeded`/`Failed`, `StartedAt`/`EndedAt`, usage fields (tokens, cost —
      nullable); migration; BR-001 index filter untouched (terminal states excluded by
      construction); matching + run-now suites gain the runs-again-after-terminal case.

## 2. The worker executes

- [x] 2.1 The worker composes modules like the Server; claim → load → Executing → invoke →
      terminal state with timestamps + usage-or-unknown. Credentials resolved by name at
      execution (D1); missing Run logs and continues.
- [x] 2.2 Functional tests with a fake IAgentRuntime at the seam: success, failure, absent
      usage → unknown, missing Run no-op, secret-name-only assertions.

## 3. The image

- [x] 3.1 Dockerfile gains node + pinned CLI; local build proves the CLI is invocable in the
      container (version check — no credential needed for that much).

## 4. Close-out

- [x] 4.1 Guardrails green (no module reaches the runtime implementation); ARCHITECTURE.md
      agent-execution section; full suite; CI green.
