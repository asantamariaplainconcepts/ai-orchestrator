# Design: output-label-set

## D1 — A Postgres array, not a join table and not a delimiter

The set belongs to exactly one Automation, is read whenever the Automation is read, is never queried
independently, and is small. That is the shape a column holds, not a table.

`text[]` rather than a delimited string, because a delimiter is a value a label could contain and a
parser is a thing that can disagree with the writer. Npgsql maps `List<string>` to `text[]` natively,
so this costs no configuration and no serializer.

A join table was the alternative and buys nothing here: an extra aggregate boundary, a cascade to get
right, and an ordering column — for a collection whose whole lifecycle is "loaded and saved with its
parent". It would be the right answer the day a label needs attributes of its own; nothing suggests it
will.

**The migration rewrites rather than drops.** EF's generated version for a type change is
drop-and-add, which would silently discard every configured hand-off in the deployment. The `Up` is
hand-written: add the array column, `UPDATE … SET output_labels = ARRAY["OutputLabel"] WHERE
"OutputLabel" IS NOT NULL`, then drop the old one. The `Down` reverses it by taking the first element,
which is lossy and says so — the only honest reverse of a widening.

## D2 — Every label is applied; the failures are collected, not the first one

Today one label lands or the Run fails saying why. With a set the question is what happens when the
second of three cannot be ensured.

**Every label is attempted, and the Run fails naming all the ones that did not land.** Stopping at the
first refusal would apply an arbitrary prefix of the set and tell the human about one problem when
there might be three — and the acceptance criterion is that a label the vendor could not ensure is
*reported*, which a message about a different label is not.

The consequence, stated because it is new: a Run that fails at hand-off may already have handed on
through the labels that *did* land. That is visible on the Story, it is what any partially-applied set
means, and the alternative — silence about the failures — is worse. It is also why the failure message
names the labels rather than counting them.

## D3 — The order labels are named is not a priority

Two output labels can match two triggers. BR-001 allows one active Run per Story, so the second
simultaneous match is ignored rather than queued.

It would be tempting to say "the first label named wins". It does not, and claiming so would be a
promise the product cannot keep: the labels are written to the vendor, come back as webhook
deliveries, and are matched then — the ordering of those deliveries is the vendor's, not ours.

So order is display order and nothing more, and the ceiling is stated where the canvas explains its
edges: **branches serialize; simultaneous matches do not queue.** A canvas that draws two edges
without saying this teaches its reader that both run.

## D4 — Dedupe case-insensitively, because the vendor does

DEC-056 already settled that `AI:Implement` and `ai:implement` are one label to the vendor, and BR-003's
trigger identity is enforced that way. A set that treated them as two members would apply the same
label twice and draw two edges to one node.

Dedupe preserves the first spelling entered, because that is the one the Admin typed and the one the
suggestions will show them next time.

## D5 — The picker suggests what is wirable, and refuses what is circular

A bare textbox asked the Admin to remember the exact spelling of another Automation's trigger — the
single most common thing to put here, and the one thing the product already knows.

So the input suggests the trigger labels of the project's **other enabled** Automations, and still
accepts free text: a mark like `ai:done` is not a trigger, and a trigger that does not exist yet is a
legitimate way to build a workflow forwards.

Disabled Automations are not suggested. Wiring an edge into something switched off produces a hand-off
that goes nowhere, which is exactly the dangling state the canvas already warns about; suggesting it
would be the product proposing its own warning.

The Automation's own trigger never appears (#115). The refusal stays on the server too — the input is
a convenience, and a rule that lives only in the input is a rule a direct API call does not have.

## D6 — The grill's default stays a default of the empty set

`HandOn` applies `ai:ready` when a `GrillToReady` Automation names no output (grill design D5). With a
set, "names no output" means the set is empty, and the default is unchanged.

It stays in `HandOn` rather than being written into data at creation, for the reason it was put there:
a stored default silently chains every Automation an Admin created without deciding to, and it would
then be indistinguishable from one they chose.

## D7 — What is deliberately not built

**Per-outcome labels.** A set for failure and another for cancellation is a different feature with a
different question behind it (what does "the workflow failed" mean to the next step?), and BR-004
already makes a failed Run terminal until a human intervenes.

**Parallel branches.** Making two matches both run is a BR-001 revision. This slice draws the second
edge and states that it serializes; it does not quietly change what BR-001 allows.
