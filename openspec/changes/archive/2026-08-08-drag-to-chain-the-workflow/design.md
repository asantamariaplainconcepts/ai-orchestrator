## Context

The pipeline is a derived graph: an edge exists exactly where one Automation's output label equals
another's trigger label, and nothing about the shape is stored (#116, design D1). #137 already put
one gesture on that picture — dragging a human block into a gap, which clears an output label. This
change adds the other direction: dragging an Automation into a gap, which rewrites two.

Everything below follows from one constraint the existing design already fixed and this change does
not get to revisit: **a gesture may only change labels.** The moment a drop stored anything about
shape, the picture could disagree with what would fire.

## Goals / Non-Goals

**Goals.** Chain and unchain from the picture. Say what a drop will do before it happens, and why a
drop cannot happen where it cannot. Show the board the workflow produces, beside the workflow.

**Non-Goals.** Touch drags. Editing from the preview. Storing shape. Multi-select. Any new rule —
this change adds none and enforces none.

## Decisions

### D1 — The slot says the wiring, before the drop

A drop between two steps rewrites two labels. A person about to change somebody's live pipeline
should be able to read which two, and read them at the gap they are hovering rather than in a
summary elsewhere.

So each slot renders the sentence its own drop would perform — "ai:grill will hand to ai:estimate ·
ai:estimate will hand to ready-for-proposal" — composed from the three Automations involved. The
end slot performs one rewrite and says one clause, because naming the dragged step twice read as
two hand-offs when there is one.

*Alternative rejected — highlight the slot and explain after.* It is the shape most drag UIs take
and it teaches the rule one gesture too late, which is the same objection #279 raised about
failures that arrive as symptoms.

### D2 — A refusal is a state of the slot, not an outcome of the drop

Four things stop a drop: a step handed to itself, an edge that already exists, a trigger shared
with another enabled Automation (BR-003), and a loop. All four are knowable while the drag is in
flight, so all four are computed per slot and rendered there, and a refused slot never accepts the
drop.

Note what this is not: **enforcement**. The update endpoint applies BR-003's overlap check and
#115's self-trigger refusal to whatever this produces, exactly as it does to an edit typed into the
panel. This is explanation, sited where the person is looking. A rule enforced in two places
eventually disagrees with itself; a rule explained where it applies does not.

The loop check walks output labels, which is what the graph is derived from — a reachability test
on anything else could refuse a drop the picture would have allowed.

### D3 — The rules are pure functions, because the gesture cannot be tested

Playwright cannot perform an HTML5 drag; #110 recorded that, and it is why #137's gesture has never
been under test. This change does not solve that, so it does the next best thing: the decisions —
what a drop rewrites, and what refuses it — live in `chainDrag.ts` as functions of the Automations
alone, with no React and no DOM.

That makes them testable the moment a frontend unit runner exists. **It does not make them tested**,
which the proof section states rather than implies.

### D4 — What is in flight is held above both surfaces

A drag starts either on a catalogue row or on a step's own handle, and only one of those is inside
the canvas. The slot needs to know *which* Automation is over it to say anything useful, and
`dataTransfer.getData` is deliberately empty during `dragover` for security — a slot can see the
type and not the payload.

So the carried Automation is React state on the section that renders both surfaces. Read back from
the drag it would have been unavailable; kept in the canvas it would have been unknown for drags
that begin in the rail.

### D5 — The preview is read-only, and is the same derivation

The columns of the Backlog board *are* the workflow's triggers. So the preview is `workflowChains`
painted sideways, not a second model — a second model is a thing that can disagree, and this one
exists to show consequences truthfully.

It is read-only for the same reason. A preview that could also be wired would be a second place to
change the same thing, and the two would drift.

### D6 — Every field of an Automation is resent from one place

The update endpoint replaces the whole Automation, so a caller that omits a field clears it. The
canvas built its request inline and did not include the model, which #291 had just added — so any
gesture on the picture silently reverted a chosen model to the deployment's.

One builder now constructs the request for all three callers. That is not a tidy-up: the failure is
invisible at the call site by construction, and the only defence is that there is one site.

## Risks / Trade-offs

**A gesture no test covers.** Accepted and stated (D3). The pure functions are the mitigation
available today.

**A slot that explains but does not enforce could drift from the endpoint.** Accepted: enforcement
stays in one place, and a stale explanation refuses a drop the server would also have refused, or
allows one the server then refuses with its own sentence. Both are safe directions.

**More surface while dragging.** Every valid slot lights up at once, which is a lot of screen for a
moment. It is deliberate: the alternative is discovering where a drop is allowed by trying.

## Migration Plan

None. No schema, no endpoint, no configuration. A deployment that never drags anything behaves
exactly as before.

## Open Questions

- The catalogue row is both a button (click to edit) and a drag source. That works, but it means a
  slow click-and-move reads as a drag; worth watching whether anyone reports losing an edit click.
- Whether the preview should collapse by default once a workflow is large enough to scroll.
