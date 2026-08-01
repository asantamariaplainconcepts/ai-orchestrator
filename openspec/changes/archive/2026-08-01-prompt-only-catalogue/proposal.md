# Proposal: prompt-only-catalogue

## Why

[#162](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/162), the owner's call
reaffirmed with the costs on the table. Every built-in action leaves, and an Automation becomes
**the repository's own prompt, unbounded**.

The agent runs with the project PAT and a workspace and does whatever the prompt says — comment,
label, transition, push, open the PR, merge — exactly as the repository's own `/aio:*` commands do
on somebody's machine. The orchestrator stops writing *actions* on the agent's behalf.

#150's "what it can do is decided here" is deliberately inverted: **anything today; limitations
arrive later as per-Automation grants**, which is a named follow-up and not this change.

## What changes

- `RepositoryPrompt` becomes the only action. The other seven — `ImplementToPullRequest`,
  `RefineOrComment`, `TransitionState`, `Estimate`, `GrillToReady`, `ProposeSpec`, `SyncChange` —
  leave the enum, the form, the canvas vocabulary and the executor.
- The executor's post-hoc ceremonies go with them: no publishing a pull request after the agent
  ran, no writing a comment, no transitioning a state, no parsing an estimate. The agent did those
  or it did not.
- The one-click defaults go too, to return later as prompt-and-grant bundles.
- `RubricPath` is renamed `PromptPath` rather than removed: #150 made it how a `RepositoryPrompt`
  names its prompt file, and the first draft of this proposal had it leaving with the grill — which
  would have broken the one action that survives.
- Automations naming a removed action are deleted by the migration. Past Runs render as any Run
  whose Automation is gone already does.

## What stays, whole

Everything that makes this a product rather than a prompt runner: triggers and matching (BR-003),
one active Run per Story and the cap (BR-001/BR-002), dispatch, per-phase timeouts (BR-005), the
live log (UC-027), usage (BR-011), terminal states (BR-004), and the workflow wiring — **output
labels are still applied by the orchestrator on success** (#115/#116). That last one is the single
carve-out to "no write of its own", and it is machinery like matching rather than action ceremony.

`requiresApproval` keeps its two-phase routing (BR-007).

## What this costs, stated rather than discovered

- **Phase containment becomes a promise, not a guarantee.** "A plan phase publishes nothing" and "a
  cancelled Run produces no PR" were enforced by the executor owning the write. They are now what
  the prompt says it will do, until grants land.
- **The conversational wait goes dormant.** `GrillToReady`'s question path is the only producer of
  the `AwaitingInput` state; `ConversationGate`, `ResumeChecker` and `RunMarker` exist for it and
  nothing else, and #166's portal conversation did not take them over. After this, nothing reaches
  that state and nothing enters the inbox's "waiting for input" category.

  They are **kept**, because the issue puts "any change to Run states" out of scope. A prompt can
  ask a question by commenting; it cannot pause its own Run and resume. This is named on the issue
  so it is a decision rather than a discovery.
- **UC-016, UC-018, UC-019, UC-024 and UC-025 stay realisable and stop being implemented.** Their
  ceremonies retire; a prompt can do each of them.

## Impact

- **Breaking:** the action vocabulary, in the API and the form. Existing Automations with removed
  actions are deleted, which is safe because nothing is in production.
- **Removal is most of the diff.** The executor loses its per-action branches; the estimate parser,
  the grill's readiness machinery and the propose/sync ceremonies go with them.
- **Specs:** `automation-configuration` and `agent-execution` lose the per-action requirements and
  gain the one that replaces them.
- **A decision:** the inversion is recorded, revising DEC-026 and DEC-048 and #150's bounded-shell
  stance, with the grants follow-up named.
