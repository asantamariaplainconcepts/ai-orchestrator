# decision-records

## ADDED Requirements

### Requirement: decisions live in immutable, numbered records

Architectural decisions SHALL live in `docs/adr/NNNN-<slug>.md`, following the repository's ADR
template: context, decision, consequences (positive, negative, neutral), alternatives considered,
references. An **accepted** ADR SHALL NOT be edited to change its decision; it is superseded by a
new ADR that references it.

#### Scenario: a decision is reversed

- **WHEN** an accepted decision no longer holds
- **THEN** a new ADR is written marking the old one superseded, and the old text stays intact

### Requirement: numbers are allocated against origin/main

ADR numbers SHALL be allocated by inspecting `docs/adr/` on current `origin/main`, and SHALL be
re-verified at sync, so two changes in flight cannot claim the same number.

#### Scenario: two changes in flight

- **WHEN** two branches each add an ADR
- **THEN** the collision is caught at sync and the later change renumbers

### Requirement: lessons graduate at the second occurrence

A one-off lesson SHALL stay in the retro log. A lesson that **recurs**, or that changes how the
workflow or tooling behaves, SHALL graduate to an ADR on its **second** occurrence — not its
third. The retro entry that triggers graduation SHALL link the resulting ADR.

#### Scenario: a pattern recurs

- **WHEN** a retro entry records a lesson already present in an earlier entry
- **THEN** an ADR is written as part of that change, not deferred

### Requirement: an ADR names its evidence and its check

Each ADR SHALL cite the specific incidents that produced it, and its consequences SHALL name the
check, gate, or test that would have caught them — so decisions move toward enforcement rather
than remaining advice.

#### Scenario: an ADR without evidence

- **WHEN** an ADR states a principle but cites no incident and proposes no check
- **THEN** it is incomplete
