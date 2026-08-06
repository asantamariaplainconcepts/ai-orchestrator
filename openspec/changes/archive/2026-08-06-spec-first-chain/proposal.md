## Why

A consented spec-first setup (#269) creates six Automations that all stand alone: every prompt in
the tier ships `outputLabels: []`, so nothing hands work on — after a grill Run succeeds, a person
must apply `ai:propose` by hand — and the Automations tab draws no chain for the workflow the tier
exists to install. #269 scoped the wiring out as *"a methodology decision"*; issue
[#273](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/273) is that decision,
made at grill by the product authority: **the full chain, with the human waits carried by the gates
that already exist.** The canvas's own seeded-defaults scenario already expects grill and propose to
arrive connected — the current catalogue does not satisfy it.

## What Changes

- The workflow tier's manifest wiring gains output labels: `grill → ai:propose`,
  `propose → ai:implement`, `implement → ai:sync`. `sync`, `refine` and `status` keep none.
- `propose`, `implement` and `sync` keep `requiresApproval: true`, so every hand-off stops in the
  Inbox for a plan approval before executing — the chain's HITL points are the existing gates.
- #262's broken-hand-off marker becomes reachable again (excluding a mid-chain step from the setup
  plan marks the step it fed), and the "no edges left to exercise" notes in `planHandoff.ts` and
  `mock.ts` are replaced by the real edges.
- **Not breaking, new setups only:** the manifest is catalogue content read at setup time. Projects
  whose Automations already exist are skipped by setup as today and keep their labels untouched.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `default-automations`: one added requirement — the spec-first tier's wiring forms a single gated
  chain, stated as content the catalogue must carry rather than behaviour the code hardcodes. The
  existing wiring-as-content requirement and its duplicate-trigger refusal are untouched.

## Impact

- `src/modules/Projects/AiOrchestrator.Modules.Projects/Starter/manifest.json` — three
  `outputLabels` values.
- `src/frontend/shared/http/mock.ts` — the mock plan carries the same edges; stale outage comment
  replaced.
- `src/frontend/features/automations/planHandoff.ts` — the "currently unreachable" doc comment goes
  with the gap it documented.
- Tests: `StarterCatalogue_Should_Constraint` (unit) pins the chain; `PipelineAdoption` /
  `SetUpDefaultAutomations` functional assertions that read `outputLabels` follow the manifest.

No endpoint, schema or contract change. No `OPN-*` open.
