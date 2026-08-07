## MODIFIED Requirements

### Requirement: a Run's detail is readable, with its Plan

The portal SHALL offer a Run detail view reachable from the Runs table showing state,
timestamps, usage, output link and — when present — the Plan rendered as sanitised markdown,
with controls to approve or reject while the Run awaits approval.

**A Run's detail SHALL distinguish what the Run recorded from what is true only while it runs.**
Its output stream and its preview are live: both exist while the Run executes and neither
survives it. The view SHALL therefore derive their availability from the Run being active and
from the executing machine's own report, never from a stored field, and SHALL render no
affordance for a live surface on a Run that is no longer live.

#### Scenario: the reviewer reads the Plan where the decision is made

- **WHEN** a Member opens a Run awaiting approval
- **THEN** the Plan renders and both decisions are available; hostile markdown in the Plan is
  inert

#### Scenario: the live surfaces disappear together

- **WHEN** a Member opens a Run that has reached a terminal state
- **THEN** the detail shows what the Run recorded — state, timestamps, usage, Plan, file changes
  — and offers no live surface, neither a stream nor a preview
