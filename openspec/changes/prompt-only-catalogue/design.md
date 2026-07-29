# Design: prompt-only-catalogue

## D1 — The inversion is the decision, so it is written down as one

#150 argued that a repository prompt is untrusted text, and that a shell which grew capabilities in
response to what the prompt asked for would be a product taking instructions from its input. That
argument was correct on its own terms and it is being overruled deliberately: the owner's position is
that a team's own repository is not an adversary, and that a tool which cannot do what the team's own
scripts do is not worth wiring up.

Both readings are defensible and the difference is who the prompt's author is assumed to be. So the DEC
records the inversion explicitly — revising DEC-026's closed catalogue, DEC-048's growth lane and
DEC-057's bounded shell — and names the grants model as the mechanism that will make bounds expressible
again. What it must not read as is an accident.

## D2 — The executor loses its ceremony, not its guarantees

Most of `RunExecutor`'s 1035 lines are ceremony: prepare a workspace, run a phase, publish a pull
request, write a comment, set a state, apply an estimate, read a rubric, resume a conversation. All of it
existed because the product decided what each action *meant*. With one action whose meaning lives in the
repository, none of it has an owner here.

What stays is everything that is true of a Run regardless of what the Run does: the two-phase routing
BR-007 requires, the phase budget BR-005 bounds, the cancellation boundary, the log streaming, the usage
record, the terminal-state transitions. Those are properties of *running work*, not of *this work*, which
is exactly why they survive an inversion this large.

The shape after the change is one path: resolve the prompt, prepare a workspace, hand the agent the PAT
and the AI credential, take the outcome from its result. No branch on action, because there is no
vocabulary left to branch on.

## D3 — HandOn goes, and the pictures that depended on it go with it

`HandOn` writes the next trigger label when a Run succeeds. It is the orchestrator acting on the agent's
behalf, which is the thing this change removes, so it goes.

The honest consequence is that three surfaces merged today lose their subject:

- `workflowGraph.ts` builds chains by matching one Automation's `OutputLabel` to another's trigger. With
  the column gone there is nothing to match, so the canvas has nothing to draw.
- The human-review block (#137) exists to *clear* a preceding step's output label. With no output label
  there is no chain to break.
- The board's chain-ordered columns (#128) ordered by that same graph.

Retiring them is not collateral damage to be minimised — it is the change being honest. A canvas that
draws a pipeline the product cannot execute is worse than no canvas, because it tells a reader the
product still chains work when it does not. The board keeps its columns and returns to the ordering it
had before #128 derived one from the graph.

`OutputLabel` leaves the schema rather than being kept "in case": a column nothing reads is a column that
drifts, and the grants follow-up will reintroduce hand-off as something the prompt does, not something
the row declares.

## D4 — The migration deletes rather than translates

Automations naming a removed action cannot be rewritten into repository prompts: there is no prompt file
to point them at, and inventing a path would produce Automations that fail at their first Run.

Nothing is in production, so the migration deletes them and drops `OutputLabel`. Past **Runs** are
untouched — they already render when their Automation is gone (BR-014's audit trail keeps the Run's own
record), so history stays readable without keeping dead configuration alive to prop it up.

## D5 — The two promises that stop being ours

The approval gate promised that a plan phase publishes nothing, and cancellation promised that a
cancelled Run produces no pull request. Both were keepable because the *executor* did the publishing: it
could hold the workspace and refuse to push.

With the agent publishing, neither is enforceable in this codebase. They become prompt-level promises —
true if the repository's prompt honours the phase it is told it is in, false otherwise, and unverifiable
either way until grants can say "this Automation may not push during planning".

This is the cost the issue put on the table and the owner accepted. It is recorded in the DEC in these
words rather than as a nuance, because someone will later read the approval gate's spec and wonder why it
promises something no code defends.

## D6 — Unreachable is cheaper than removed, for Run states only

Once the grill is gone nothing puts a Run into `AwaitingInput`, and the conversational resume path (#78)
has no producer. It stays: the issue puts Run states, dispatch and matching out of scope, and deleting a
state plus its resume loop in a change already this wide would mean touching the state machine while its
only reader is being rewritten.

The distinction against D3 is worth stating: the canvas is *user-visible* and would lie, while an
unreachable state is invisible and merely idle. Lying costs a reader's understanding; idling costs
nothing until someone needs the state again — which the grants follow-up may well.
