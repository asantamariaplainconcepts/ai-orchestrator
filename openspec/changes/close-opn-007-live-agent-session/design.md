## Context

The conversational loop is half-built and specified for a shape nobody enjoys using.
`ConversationGate` can ask (`AskAndWait`) and read answers (`AnswersFor`), `ResumeChecker` resumes a
waiting Run, and `AwaitingInput` is a real state with a real inbox behind it — but the asking side has
**no production caller** (the gate's own summary says so: "the grill action (#79) is its first
consumer, deliberately a separate change"), and there is no portal answer box, so every answer is a
comment typed into GitHub's own UI and every exchange is a full agent pass.

[ADR-0008](../../../docs/adr/0008-a-live-conversation-costs-a-pass-per-message.md) chose that shape
deliberately and rejected a live session. Its reasoning is intact as reasoning; two of its three
premises have since changed (DEC-013 superseded by #296; "nothing idles" revised by DEC-061 and
DEC-060), and two facts it could not have weighed now exist: a self-host habitat where holding a
sandbox costs no money, and a working spike proving the transport.

This change is the decision, not the capability. What it must not do is decide by drift — reaching a
conclusion because a spike happened to work.

## Goals / Non-Goals

**Goals:**

- Make the live-session question decidable: state the candidate shapes and the criteria each is judged
  against, so the recorded outcome is a decision rather than a preference.
- Produce an ADR that supersedes or reaffirms ADR-0008, per the `decision-records` spec's rule that an
  accepted ADR is never edited to change its decision.
- Record and close OPN-007 in the same change, and fix the file's stale "None remain open" claim.
- Leave the spec able to state, in one requirement, how a human supplies input to a waiting Run —
  which today is implied by an ADR rather than specified.

**Non-Goals:**

- Building the capability, in any shape. No terminal UI, no `IAgentProcessHost` change, no hub.
- `Automation.Interactive` — the declarative flag that labels an Automation as needing a human. It
  survives either outcome and is its own slice.
- Authorization and audit for an attached session. A shell inside a Run's sandbox is arbitrary command
  execution against a sandbox carrying the machine owner's own session (#288); it needs a grant of its
  own rather than `RunPermissions.Read`, and that is a separate decision with its own risk.
- The portal answer box. It is ADR-0008's named follow-up and is worth building whichever way this
  goes — reaffirming ADR-0008 makes it *the* answer, and superseding ADR-0008 still leaves it the
  durable path for a human who is not present.

## Decisions

### D1 — Supersede, never amend

If the conclusion differs from ADR-0008, a new ADR marks it superseded and ADR-0008's text stays
intact. This is not a preference: the `decision-records` spec requires it, and ADR-0008 itself names
supersession as the route. The number is allocated against current `origin/main` and re-verified at
sync, because two changes in flight cannot claim one number.

### D2 — Three candidate shapes, judged against the same criteria

Naming the alternatives before evaluating them is what keeps the spike from deciding by itself.

1. **Reaffirm ADR-0008.** A pass per message; the human answers in the portal or on the Story. The
   attached session stays refused, and the spec says so explicitly instead of leaving it implied.
2. **Permit an attached session, bounded by inactivity.** The DEC-061 shape, already accepted for
   portal conversations: the sandbox is held while a human is attached and reclaimed after a cooldown.
   BR-006 survives because the cooldown times the *container*, not the person — the same argument
   DEC-061 already won.
3. **Split by habitat.** Permit it in self-host (sbx on the owner's hardware, no per-hour cost) and
   refuse it in a deployment, or vice versa. Legitimate, and the most likely to be reached by accident
   rather than on purpose, which is why it is listed.

Criteria, applied to each: BR-006 (is any clock put on the human?); BR-005 (does the agent's own
kill-on-timeout keep its meaning?); BR-001 (does a waiting Run still block its Story?); cost, stated
per habitat rather than in general; the credential boundary DEC-030 rests on (one session, one
container, one project's PAT); and **transcript integrity**, below.

### D3 — Transcript integrity is a first-class criterion, and it is new

Both runtimes are invoked headless with structured output — `claude -p --output-format stream-json`
and `opencode run` — and #299/#300 just built the Run's Output on that stream, rendering it as a
transcript of steps rather than a JSON dump. **An attached interactive session replaces that stream
with a terminal byte-stream.** Escape sequences and cursor movement are not a transcript; they are a
screen recording. So "the human types into the agent" costs the product a record it just finished
building, while "the human works beside the agent in the same sandbox" costs nothing — the agent's
structured stream is untouched and the human's shell is a second, separately recorded stream.

ADR-0008 could not have weighed this: the transcript did not exist in July. It is the strongest
technical argument in the whole analysis, and it points at a distinction the issue's framing does not
yet make — *attach to the agent* and *attach to the agent's sandbox* are different capabilities with
very different costs.

### D4 — The spike is evidence of feasibility, and of nothing else

What it proved: a host-allocated pty, `sbx exec -it`, signals, geometry, full-screen programs. What
it did not address: authentication, audit, what a second writer in the workspace does to the agent's
own working tree, or cost in a deployment. The ADR cites it for what it measured and no further.

### D5 — The spec delta is written with the ADR, not before it

The requirement text differs per outcome, so this change writes the delta after the decision within
the same change. Reaffirming ADR-0008 still produces a delta: it turns an implication into a stated
requirement, which is the gap that let this question stay open.

## Risks / Trade-offs

- **Deciding by momentum** → the criteria in D2 are applied to all three candidates in the ADR's own
  text, and the rejected ones are recorded with their reasons, so a reader can see the alternatives
  were weighed rather than skipped.
- **A second writer corrupts the agent's work** — a human running `git checkout` in a sandbox while
  the agent edits the same tree → out of scope for the decision, but the ADR must name it as a
  consequence rather than discover it later; it is a reason a permissive outcome still needs its own
  capability slice.
- **A held sandbox leaks** → the precedent is measured and ugly: 31 sandboxes and 125 GB on this
  developer's machine, because a process died before its `finally` ran. Any outcome permitting a held
  sandbox inherits that failure mode and must name what reclaims it (`ReapAbandoned` claims the
  `aio-*` namespace today).
- **ADR number collision with another change in flight** → allocated against `origin/main`, re-verified
  at sync; the `decision-records` spec already carries this rule.
- **Reaffirming looks like wasted work** → it is not: it converts an implied constraint into a
  specified one and closes the question with reasons, so the next person with this idea reads the
  analysis instead of relitigating it.

## Migration Plan

No runtime change, so nothing to deploy or roll back. The change is documents plus spec deltas; its
"rollback" is a superseding ADR, which is the mechanism under discussion.

## Open Questions

- **The decision itself** — which of D2's three shapes, and on which pillar. This is the work.
- **Attach to the agent, or beside it?** D3 argues these are separate capabilities and that the second
  is far cheaper. The ADR should answer both, because permitting one and not the other is a coherent
  and probably correct outcome.
- **Does the answer differ by habitat?** Required to be stated either way (D2.3).
- **Does the follow-on capability need `Automation.Interactive` at all?** If a human can attach to any
  Run on demand, a declarative flag may be redundant — or may remain useful for the inbox's framing.
  Noted for the follow-on issue, not decided here.
