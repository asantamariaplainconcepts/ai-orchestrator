# Design — automation-editing

## D1 — One overlap gate, three callers

`Automation.Overlaps` and the "no enabled sibling may intersect" query already exist for create
(#14). Edit and enable call the same query with one addition: the subject is excluded by id.
Re-deriving the rule per operation is how two of the three end up subtly different — the
subsumption case (#14's whole finding) is exactly what a second implementation would miss.

## D2 — Enabling is a create-shaped act; disabling is not

BR-003 speaks about *enabled* Automations. Disabling can never introduce an overlap, so it is
unconditional. Enabling can, because the world moved while it was off, so it re-checks. Treating
them symmetrically would either block harmless disables or admit overlapping enables.

## D3 — In-flight Runs are already insulated, and the test says so

A Run stores `AutomationId` and reads details through `IAutomationCatalog` at execution. An
edited Automation therefore changes future executions, and a *disabled* one makes
`Detail` return null — which the executor already treats as a stated failure rather than a
crash. The new test pins the behaviour that matters: an active Run is not retroactively broken
by editing its Automation.

## D4 — The response shape stays the create response

Edit returns what create returns. A separate "updated" shape would drift from it, and the
frontend already renders one.
