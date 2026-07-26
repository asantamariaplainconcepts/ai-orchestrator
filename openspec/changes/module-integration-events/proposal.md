# Proposal: module-integration-events

## Why

Issue #41, which blocks #17 and transitively #19–#28. No mechanism exists for a module to learn
that something happened in another: BuildingBlocks has no event seam, and the `.Contracts`
pattern the guardrails ratified at bootstrap has zero implementations. Matching (#17) needs both
— Backlog must announce that a Story's labels changed, and the Runs module must read Projects'
Automations to match against.

The corpus already chose the shape: UC-009 says polling *"emits normalized story events"*
(DEC-028), and BR-015 requires webhook and polling events to be *identical before matching*.
This change builds what those sentences assume exists.

## What changes

- **Integration events over CAP (DotNetCore.CAP), wrapped.** `IIntegrationEventPublisher` and
  `IIntegrationEventHandler<T>` live in BuildingBlocks; the CAP implementation lives in
  ServiceDefaults — the same placement as the secret resolver and the run dispatcher, and for
  the same reason: modules reference no infrastructure SDK, and the guardrails keep enforcing
  that instead of being widened.
- **Transactional publish (outbox).** CAP persists published messages in Postgres in the same
  transaction as the publishing module's `SaveChanges`, so an event cannot be lost between a
  commit and a crash. Transport is in-memory (in-process delivery); persistence is what makes
  that safe.
- **The first real event: `StoryChanged`.** Published by the Backlog reconciler when a Story is
  created or its labels/state change — the "normalized story event" UC-009 names. Defined in
  **`AiOrchestrator.Modules.Backlog.Contracts`**, the first Contracts assembly, proving the
  ratified pattern.
- **CAP's schema is a migration concern.** Its tables are initialised by the MigrationService,
  never by an app at startup — the invariant two earlier changes established stays true.
- **Consumers are idempotent by contract.** At-least-once delivery is the deal; the spec says so
  and #17's matcher will lean on a BR-001 partial unique index as its floor.
- **Guardrail extension:** ArchTests gain the rule that a module may reference another module's
  `.Contracts` assembly and never its implementation.

## What this deliberately does not do

Matching, Run creation, the Runs module itself (#17 — this change gives it the mechanism, not
the behaviour). Distributed transport (the CAP transport swap is a later, deliberate change if
the monolith ever splits). Webhooks (#31 — they will publish the same event type, which is
BR-015 holding by construction).

## Verified preconditions become verified claims

Task 0 is a spike: **prove CAP with in-memory transport + Postgres storage redelivers after a
process kill** — publish, kill before the consumer completes, restart, observe redelivery. The
design treats crash-survival as a hypothesis until that runs (ADR-0005). If the spike disproves
it, the transport moves to CAP's Postgres-backed queue mode and the design notes the cost.

## Impact

- BuildingBlocks: two interfaces, no dependencies added.
- ServiceDefaults: CAP packages, the wrapper, composition extensions.
- Backlog: publishes `StoryChanged` inside its reconciliation transaction; gains a Contracts
  project with the event record.
- MigrationService: initialises CAP storage.
- Specs: new `module-integration` capability.
