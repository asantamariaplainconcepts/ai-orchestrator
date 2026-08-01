# automation-configuration — delta

## ADDED Requirements

### Requirement: bulk creation converges instead of colliding

The one bulk-creation path (default-automations setup) SHALL reuse the single-Automation
creation semantics — the same validation, the same BR-003 normalised-trigger comparison — and
SHALL treat losing a uniqueness race as "already exists, skipped", never as a failure surfaced
to the Admin. Convergence is the promise: after the action, the wired set exists exactly once
regardless of what existed before or ran concurrently.

#### Scenario: a concurrent duplicate is a skip, not an error

- **WHEN** two set-up-defaults requests race on one project
- **THEN** both answer successfully, each trigger exists exactly once, and the union of the two
  responses' created+skipped lists covers the whole wired set
