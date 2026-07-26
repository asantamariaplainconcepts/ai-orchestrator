# Tasks — module-integration-events

The spike first — the design's crash-survival claim is a hypothesis until it runs (ADR-0005).
Then the seam, the first event, and the guardrails that keep the boundary honest.

## 0. The spike: prove redelivery

- [ ] 0.1 A throwaway console pair against Postgres: publish via CAP (in-memory transport +
      Postgres storage), kill the process before the consumer completes, restart, observe.
      **Pass:** the event is delivered after restart. **Fail:** switch the design to CAP's
      storage-polling transport and record the latency cost in design D3. Either way, write the
      observed behaviour into D3 — observed, not assumed.
- [ ] 0.2 In the same spike: confirm CAP's storage init can be disabled app-side and run
      standalone (for the MigrationService). If it cannot, record D5's fallback as active.

## 1. The seam (BuildingBlocks)

- [ ] 1.1 `IIntegrationEvent` (Version), `IIntegrationEventPublisher`,
      `IIntegrationEventHandler<T>` — product vocabulary, no CAP type anywhere in the signatures.
- [ ] 1.2 Verify: BuildingBlocks gains no package reference; modules still reference no
      infrastructure SDK, transitively checked as in #16.

## 2. The CAP implementation (ServiceDefaults)

- [ ] 2.1 CAP packages pinned in CPM; publisher implementation; the generic subscriber that
      receives topics and fans out to registered `IIntegrationEventHandler<T>`s.
- [ ] 2.2 Composition: `AddIntegrationEvents()` for hosts — Postgres storage, in-memory
      transport, **deliberate small retry ceiling** (D4, not the ~50 default), storage init off.
- [ ] 2.3 MigrationService initialises CAP storage in schema `cap` (D5).
- [ ] 2.4 Verify by artifact: after the MigrationService runs, the `cap` schema exists; after
      the Server starts, no new tables appeared (the Server still migrates nothing).

## 3. The first event

- [ ] 3.1 `AiOrchestrator.Modules.Backlog.Contracts`: `StoryChanged` (version, project id,
      vendor story id, change kind: Added/Updated/Removed). No implementation types.
- [ ] 3.2 The reconciler publishes inside its transaction: one event per changed Story, nothing
      on a no-op poll. The concurrency path (#7's duplicate-key catch) publishes nothing extra —
      the winner's transaction already announced.
- [ ] 3.3 Functional tests against real containers: a label change delivers exactly the fact
      that changed; a no-op poll delivers nothing; **the rollback case** — force a failure after
      publish, assert no delivery (the transactional-publish scenario, which is the entire point).
- [ ] 3.4 A crash-redelivery functional test if the harness can express it honestly; otherwise
      the spike's evidence is linked from D3 and the gap is stated, not papered over.

## 4. Guardrails

- [ ] 4.1 ArchTest: a module referencing another module's implementation assembly fails; the
      same module referencing its Contracts assembly passes. Both directions asserted with the
      real Backlog.Contracts in place.
- [ ] 4.2 Full suite green; the analyzers untouched (Contracts support already exists — verify,
      do not re-implement).

## 5. Close-out

- [ ] 5.1 ARCHITECTURE.md: the integration-events section — transactional publish, at-least-once,
      idempotent consumers, where contracts fit, and why modules never see CAP.
- [ ] 5.2 Full verify sweep; CI green.
