# Design: delete-automation

## D1 — The refusal is the feature

"Delete what was never used, disable what was" is one sentence an Admin can hold, and it is
derivable from rules that already exist rather than invented for this change. The refusal names
the count, so the message teaches the rule instead of merely enforcing it.

Rejected: cascade-nulling the Runs' `AutomationId`. BR-014 lists the Automation among what every
Run records; a Run whose Automation is null is a Run whose provenance was deleted to make a
button work.

## D2 — The usage question crosses a module boundary, so it crosses through Contracts

The Runs module owns Runs; the Projects module owns Automations and hosts the deletion. Projects
therefore asks Runs, through a new `AiOrchestrator.Modules.Runs.Contracts` carrying one method.
Runs already consumes Projects' contracts; a Contracts-to-Contracts edge is not a cycle, and the
guardrail suite is what proves the implementation assembly stayed unreferenced.

Rejected: putting the count in the Projects schema as a denormalised counter. It would be a
second source of truth for something the Runs table already answers exactly.

## D3 — Deleting frees the trigger, which is BR-003 behaving normally

A deleted Automation's trigger label becomes available: the overlap guard queries live rows, so
nothing special is needed. Worth an acceptance criterion anyway, because "can I now recreate it"
is the first thing anyone does after deleting something by mistake.

## D4 — Scoped by project, always

The delete resolves on `(projectId, automationId)` like every other Automation operation. An id
from another project is "not found", not "forbidden": a tenant should not learn what exists
elsewhere from an error message.
