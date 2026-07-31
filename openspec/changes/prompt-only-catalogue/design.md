# Design: prompt-only-catalogue

## D1 — The enum keeps one member rather than disappearing

`RepositoryPrompt` could have become implicit: no action field at all, since there is only one.

It stays as a one-member enum because the field is the place a second thing will be said. Grants are
the named follow-up, and "which prompt, under which grants" belongs beside "which trigger" — a shape
that already exists is easier to extend than one that has to be reintroduced. It also keeps the API
and the stored rows readable: a row saying `RepositoryPrompt` explains itself, and a row with the
column removed relies on a reader knowing what year it was written.

The refusal for the removed names is the ordinary unknown-action one. No special "that action was
removed" message: the vocabulary is what it is now, and a caller sending `Estimate` is sending
something that is not an action, which is exactly what the existing refusal says.

## D1a — The rubric path is renamed, not removed

The proposal's first draft said the rubric path left with the grill. It was wrong, and reading the
executor is what caught it: #150 made that same column the way a `RepositoryPrompt` names its prompt
file. Removing it would have deleted the only field the surviving action needs, and the failure would
have been every Run resolving an empty path.

So it is renamed to `PromptPath`. "Rubric" was the grill's word for a readiness bar; what the column
holds now is the name of a prompt, and a field whose name describes a deleted feature is a field the
next reader mistrusts. The migration renames the column and keeps every value.

What does go is the other half of that requirement: the grill's default ready label. With no grill,
nothing defaults an output label, and an Automation that names none hands nothing on.

## D2 — The migration deletes rather than converts

An Automation naming `Estimate` cannot become a `RepositoryPrompt` one: there is no prompt file to
point it at, and inventing a path would produce an Automation that matches Stories and then fails on
every Run — worse than one that is gone.

Nothing is in production, which the issue states and which makes this safe rather than merely
convenient. Past Runs are unaffected: a Run whose Automation no longer exists already renders, because
#116's delete path required exactly that.

## D3 — The output label stays the orchestrator's, and that is the only carve-out

"The orchestrator performs no vendor write of its own" would, read literally, take the output label
with it. It does not, and the reason is what separates machinery from ceremony.

Matching, the cap, the timeout and the output label are all things the *orchestrator* is for: they are
true of every Automation regardless of what its prompt says, and a prompt that had to apply its own
output label would be a prompt that has to know about the workflow it sits in. Publishing a pull
request is different — it is one action's ceremony, performed on the agent's behalf, and it is
precisely what this change is removing.

Stated in the decision rather than left to the reader, because "no writes except this one" is the
kind of sentence that looks like an oversight when it is a choice.

## D4 — Phase containment degrades honestly

BR-007's two phases still route, and the executor still refuses to run phase two before somebody
approves. What it can no longer do is *guarantee* that phase one wrote nothing: the agent holds the
PAT for both phases, and the only thing standing between a planning pass and a pushed branch is what
the prompt says.

Two options were open. Keep a bounded shell for the planning phase only — rejected, because it
reintroduces the per-action containment this change exists to remove, on the phase where it is
hardest to define. Or state the degradation and let grants fix it properly — chosen.

So the approval gate is now a *workflow* control rather than a *containment* control: it decides
whether a human sees the plan before the work continues, and it no longer promises the work has not
already happened. That sentence goes in the decision, because somebody relying on the old promise
would not otherwise learn it changed.

## D5 — The conversational wait is kept, dormant, and said out loud

`GrillToReady`'s question path is the only producer of `AwaitingInput`. Removing the grill leaves
`ConversationGate`, `ResumeChecker` and `RunMarker` with no caller, a Run state nothing reaches, and
an inbox category nothing enters.

Removing them is the tidier answer and it is **out of scope by the issue's own words** — "any change
to Run states". So they stay, and the dormancy is written down here and on the issue rather than
being left for whoever next greps for a producer.

Worth recording why the obvious replacement did not happen: #166's issue expected the portal
conversation to become that producer. It did not — the conversation built there has its own path and
never touches the gate. That was the right call for #166 (a conversation is not a Run, and the gate
pauses Runs) and it is why this gap exists now rather than being closed by accident.

## D6 — What is deliberately not built

**The grants model.** Named in the issue as the follow-up, and it is the thing that makes "unbounded"
temporary. Building a half of it here would fix the phase-containment regression in the least
reviewable way.

**Seeding example prompts into repositories.** Out of scope in the issue. A project with no prompt
file gets the missing-file failure #150 already names, which says the resolved path — that is the
right first experience, and a seeded file would be the product writing to somebody's repository
uninvited.
