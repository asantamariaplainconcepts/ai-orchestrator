# module-integration

## ADDED Requirements

### Requirement: a module announces facts through integration events

A module SHALL publish integration events through `IIntegrationEventPublisher`, and the publish
SHALL be transactional with the module's own state change: the event and the data it announces
commit or roll back together. Events SHALL carry identity and change-kind, not entity state, and
SHALL carry a version a consumer can refuse. No module SHALL reference the messaging
infrastructure directly — the seam lives in BuildingBlocks, the implementation in the host
composition, and the analyzers/ArchTests keep enforcing it.

#### Scenario: the publishing transaction rolls back

- **WHEN** a module's `SaveChanges` fails after it published an event in the same unit of work
- **THEN** the event is never delivered — there is no announcement of a change that did not
  happen

#### Scenario: the process dies after commit, before delivery

- **WHEN** the process terminates after the publishing transaction committed but before a
  consumer handled the event
- **THEN** the event is delivered after restart — a committed fact is eventually announced

### Requirement: delivery is at-least-once and consumers are idempotent

Event delivery SHALL be at-least-once. Every handler SHALL tolerate receiving the same event
more than once, and a handler that cannot parse an event's version SHALL drop it explicitly
rather than fail forever. Handler retry on failure SHALL be bounded by deliberate configuration,
and exhausted retries SHALL be observable — a silent dead-letter is the telemetry failure mode
again.

#### Scenario: a duplicate delivery

- **WHEN** a handler receives an event it has already processed
- **THEN** the outcome is identical to having received it once

### Requirement: cross-module reads go through Contracts assemblies

A module needing another module's data SHALL depend on a read interface in that module's
`.Contracts` assembly, never on its implementation assembly. The owning module SHALL register
the implementation itself. Module discovery SHALL continue to skip Contracts assemblies, and the
ArchTests SHALL verify both directions: Contracts references allowed, implementation references
forbidden.

#### Scenario: the first Contracts assembly exists

- **WHEN** the Backlog module is built
- **THEN** `AiOrchestrator.Modules.Backlog.Contracts` contains the `StoryChanged` event and no
  implementation types, and the guardrail suite passes with it in place

### Requirement: the Backlog announces Story changes

The Backlog reconciler SHALL publish a `StoryChanged` event when a Story is created or its
labels or state change, inside the reconciliation transaction — the "normalized story event"
UC-009 names. Removal of a Story SHALL also be announced. A poll that changes nothing SHALL
publish nothing.

#### Scenario: a label appears on a Story

- **WHEN** reconciliation records a label change on a Story
- **THEN** a `StoryChanged` event for that Story's identity is committed with it and delivered
  at least once

#### Scenario: an unchanged mirror

- **WHEN** a poll finds nothing to change
- **THEN** no event is published — matching has nothing to reconsider
