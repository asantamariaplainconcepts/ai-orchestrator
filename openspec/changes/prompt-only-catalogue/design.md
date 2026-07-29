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

## D3 — Hand-off is the workflow, and the workflow is not what this retires

`HandOn` writes the next trigger label when a Run succeeds, and it is a vendor write the orchestrator
performs itself. The literal reading of "no writes of its own" takes it out, and taking it out would have
removed the workflow canvas, the human-review block and the board's chain ordering along with it.

DEC-053 is what settles this. It separated the **catalogue** — what a single step does — from the
**workflow** — how steps connect. #162 is a change to the catalogue: one action, decided by the
repository. The workflow is a different axis, and nothing in the issue argues against it.

The distinction that makes the rule coherent rather than bent: the writes being removed are the ones that
**complete the agent's work** — publishing its branch as a pull request, posting its reply as a comment,
turning its answer into a state or an estimate. Those existed because the product decided what the action
meant, and it no longer does. The hand-off label is not that. It is the product executing its **own**
declared configuration: this Automation, on success, hands to that trigger. The prompt did not ask for it
and cannot ask for it, which is exactly why it survives a change about what prompts may ask for.

So `OutputLabel` stays in the schema, `HandOn` stays in the executor, and the three surfaces that draw
chains keep their subject. What follows is a smaller migration — no column dropped — and a smaller
deletion in the portal.

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
