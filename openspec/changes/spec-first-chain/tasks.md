## 1. The wiring

- [x] 1.1 Set the manifest's output labels: grill → `ai:propose`, propose → `ai:implement`,
      implement → `ai:sync`; sync, refine and status stay `[]`.

## 2. The surfaces that claimed no edges exist

- [x] 2.1 Update `mock.ts`: the mock plan carries the same edges as the manifest, and the
      "no hand-off edges left to exercise" comment is replaced by one naming the chain.
- [x] 2.2 Update `planHandoff.ts`: the "currently unreachable, kept deliberately" doc comment goes
      with the gap it documented — the marker is reachable again.

## 3. Tests

- [x] 3.1 Pin the chain in `StarterCatalogue_Should_Constraint`: the workflow tier's wiring forms
      the linear chain above, refine/status carry no labels, and no label names a trigger outside
      the tier.
- [x] 3.2 Re-run the functional suites that read `outputLabels` from the manifest
      (`PipelineAdoption`, `SetUpDefaultAutomations`) and update any assertion pinned to the old
      empty values.

## 4. Verification

- [x] 4.1 `dotnet csharpier check src` clean; `pnpm format:check`, `lint`, `typecheck` clean from
      `src/frontend`.
- [x] 4.2 `dotnet build src/AiOrchestrator.slnx` — 0 errors.
- [x] 4.3 Functional + unit suites for the Projects module pass locally.
- [x] 4.4 In mock mode: the setup plan shows the edges, excluding a mid-chain step marks its
      downstream (the #262 marker, reachable by hand again), and the built workflow draws one
      chain with three gates.
- [ ] 4.5 CI green on the PR head (verified job-by-job), at sync.
