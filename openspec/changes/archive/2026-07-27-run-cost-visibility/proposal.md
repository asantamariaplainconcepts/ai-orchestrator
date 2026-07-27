# Proposal: run-cost-visibility

## Why

Issue #25 (UC-020, surfacing through UC-021). #18 already persists tokens and cost on the Run
and already nulls a missing report — so the *persistence* half shipped. Nothing has ever read
it: the API omits the fields and the Cost column has been an em-dash since #20. This is the
reading half, and it is the last unfilled column in the Runs table.

The sharp edge is new since #30: free models make **$0.00 a real value**, so "unknown" and
"free" must not render the same way. A cost display that shows zero for both is worse than no
display, because it quietly asserts something untrue.

## What Changes

- **The runs API exposes** input tokens, output tokens and cost — nullable, because null means
  "the runtime did not tell us" (BR-011).
- **The Runs table's Cost column** shows the amount, or the empty value with "unknown" where
  nothing was reported; the Run detail page adds the token counts.
- **The project page totals** the cost of its Runs, and states how many Runs are excluded as
  unknown so the total is never quietly understated.
- **Money is formatted as money** and tokens are tabular, so a column can be scanned.

## Impact

- Affected specs: `run-orchestration` (the reading requirement).
- Touched: Runs module (ListRuns response, a small project-total read), frontend (Runs table,
  Run detail, project page, catalog), Runs functional tests.
- Out of scope: budgets, caps, alerts, per-period reporting, currency conversion.
