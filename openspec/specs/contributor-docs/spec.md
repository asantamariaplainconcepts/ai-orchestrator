# contributor-docs Specification

## Purpose
TBD - created by archiving change ceremonies. Update Purpose after archive.
## Requirements
### Requirement: CONTRIBUTING describes the loop for humans and links its mechanics

`CONTRIBUTING.md` SHALL describe the loop end to end for a human contributor: the nine states,
the two review gates, one issue = one branch = one PR, and how a change reaches `main`. It SHALL
link to the `/aio:*` command files for gate mechanics rather than restating them, so a command
change cannot silently orphan the prose.

#### Scenario: a gate's mechanics change

- **WHEN** a command's precondition changes
- **THEN** `CONTRIBUTING.md` remains accurate because it links rather than duplicates

### Requirement: the solo path and the spec-less lane are documented, not folklore

`CONTRIBUTING.md` SHALL state the solo-maintainer review path (DEC-016: GitHub forbids
self-approval, so the recorded gate is the label transition plus the PR checklist) and the
spec-less lane (DEC-025: `lane:spec-less`, retro still mandatory, nothing to archive).

It SHALL also state the **unattended route** (DEC-068, ADR-0027): `/aio:ship` carries a ready issue to
`main` in one run with no review stage, the invocation is the recorded authorisation in place of
DEC-016's in-session go-ahead, a halt applies the hold and hands back to a person, and the staged
route remains the default. It SHALL name what the route gives up — that no human reads the spec or the
diff — rather than presenting it as a faster equivalent.

#### Scenario: a contributor hits the self-approval wall

- **WHEN** someone tries to approve their own PR to satisfy the sync gate
- **THEN** `CONTRIBUTING.md` already tells them what the recorded gate is instead

#### Scenario: a contributor finds an unreviewed change on main

- **WHEN** someone reads a merge commit whose PR says it was shipped unattended
- **THEN** `CONTRIBUTING.md` already explains the route that produced it, what authorised it, and
  when it is appropriate — so an unreviewed merge is documented practice rather than an anomaly to
  reconstruct

### Requirement: onboarding is short because the docs carry the weight

`ONBOARDING.md` SHALL take a newcomer from zero to a first contribution in **no more than 40
lines**: orient, run it, first contribution through the loop, and the guardrails that will
otherwise surprise them. It SHALL link to the canonical home of every fact rather than
summarising it, and SHALL point agents at `AGENTS.md`.

#### Scenario: onboarding grows

- **WHEN** onboarding would exceed 40 lines
- **THEN** the fact moves to its canonical home and onboarding links to it

#### Scenario: onboarding friction is a defect

- **WHEN** a documented setup step fails for a newcomer
- **THEN** that is filed as an issue rather than worked around silently

### Requirement: one canonical quick-start

Setup commands SHALL live in `README.md` only. `ONBOARDING.md` and `CONTRIBUTING.md` SHALL link
to it and SHALL NOT repeat the commands.

#### Scenario: the dev loop command changes

- **WHEN** the one-command dev loop changes
- **THEN** exactly one document needs editing

