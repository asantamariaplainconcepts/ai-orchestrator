# Design — run-visibility

## D1 — The API returns what the Run records; the UI composes the rest

The Runs endpoint exposes the BR-014 subset that exists (automation **id**, not its details).
The automation's label/action/runtime already have an owner and an endpoint
(`GET /api/projects/{id}/automations`); the page joins the two client-side. Widening
`AutomationTrigger` (Projects.Contracts) for display would grow the matching surface for a
read the frontend can already make — the Contracts record stays "what matching needs and
nothing more". A Run whose Automation has since been deleted or disabled renders its columns
as the empty value: the Run is the historical record, the Automation is current config, and
pretending they always join is the kind of lie BR-014 exists to prevent.

## D2 — Columns without a producing feature render the empty value, and the shape is stated

Output link, logs and cost are DEC-031's full picture; nothing produces them yet. The columns
exist now with the design-system em-dash so the page's final shape is visible and later issues
fill data in rather than reshaping the table. The alternative — omitting the columns — would
make #19/#25 UI changes instead of data changes.

## D3 — Newest-first by CreatedAt, GUIDv7 as the tiebreaker

Runs are born from events; the reader wants "what just happened". `ORDER BY "CreatedAt" DESC,
"Id" DESC` — the v7 id is time-ordered, so the tiebreak stays stable across equal timestamps
without a second timestamp column.

## D4 — Read-only is a property, not a habit

The slice adds exactly one GET. Nothing in it takes a lock, opens a transaction beyond the
query, or exposes a mutation path — cancel (#23) and Run now (#21) will make their own
proposals against the same table.
