# Design: prompt-scratchpad

## D1 — A scratchpad is a conversation, started fresh for each attempt

The issue left this open: one conversation whose first message is the prompt text, or a sibling that
reuses the runtime seam without the stored exchange.

**Decided: it is a conversation.** The argument is a count, not a preference. Of the twelve
acceptance criteria in #189, these are already properties of `HoldConversation.Say`, verifiable in
the code as it stands:

| #189 asks for | Where it already holds |
| --- | --- |
| one agent pass against the repository cloned with the project credential | `SayHandler` → `IConversationRuntime.Answer` with `ConversationContext` |
| the reply readable in the portal | `Response.Messages` |
| cost shown, unknown never zero | `Conversation.Spend()` → `SpendUsd` + `SpendIsComplete` |
| a named Story's context read from the mirror; naming none is ordinary | `SayHandler.StoryContext`, `VendorStoryId` nullable |
| a failure shown, the surface still usable | `conversation.Fail(...)`, no error returned |
| nothing written to any Automation | there is no such write |
| no Run, no cap slot, no Story lock | the absence is #166's stated feature |
| a caller with no role refused, disclosing nothing | `[Requires(RunPermissions.HoldConversation)]` + `IScopedToProject` |
| a Member may use it | `HoldConversation` is in the Member bundle |

A sibling capability would re-assert all nine to gain one thing: no conversation row per attempt.

**That row is worth less than it looks, and is measurably not clutter.** There is no conversation
*list* endpoint — `HoldConversation` exposes start, say, and read-by-id only. A conversation is
reachable solely by its identifier, so a scratchpad row appears on no surface unless somebody holds
its id. It is not litter; it is where the money that was spent is recorded.

**Fresh per attempt, not one long conversation.** A conversation keeps its history and, where the
habitat provides a session host, its warm container (DEC-061). A second attempt at an *edited* draft
inside the same conversation would reach an agent that has already seen the first draft and its own
reply — so the trial would no longer predict the Run, which is the only reason the scratchpad exists.
Each attempt therefore starts a conversation and sends one message. The frontend already has both
calls; no new endpoint is needed for this.

### Alternative rejected

*A sibling `TryPrompt` slice calling `IConversationRuntime` directly with no persistence.* It gives
up the cost record — and BR-011's argument ("unknown is not zero") is an argument about honesty
regarding money that was actually spent, which applies with full force to a draft. A capability that
spends and then forgets is the one shape this product has consistently refused.

## D2 — The same permission, not a new one

`RunPermissions.HoldConversation`, unchanged. A scratchpad attempt spends exactly what a conversation
message spends, against the same repository, with the same credential. A second permission would have
to be justified by a difference in consequence, and there is none. #189 states the same conclusion in
its acceptance criteria; this records that it is satisfied by *not* adding anything.

## D3 — One description of a Story, shared by both paths

This is the decision that makes a trial predictive, and it was not in the issue — it surfaced from
reading both call sites.

Today the two paths describe the same Story differently:

```
Run (RunExecutor)                          Conversation (SayHandler)
────────────────────────────────────       ─────────────────────────────
Story #12: Title                           Title
State: open; labels: ai:refine.
                                           Body (unbounded)
Description:
Body (truncated at 8000 chars)
```

A prompt tried against the right-hand framing and then run against the left-hand one has been tried
against a different input. State and labels are exactly the sort of thing a real prompt branches on
("if this story is already labelled…"), so the difference is not cosmetic.

**Decided: one helper produces the Story description, and both callers use it** — the Run's framing,
because that is the one that must stay faithful; the conversation adopts it. Two consequences, both
wanted:

- a conversation's agent now gets state and labels it did not have;
- a conversation's Story body is now truncated at the same 8,000 characters a Run truncates at,
  where it was previously unbounded — a Story with a very long description could previously push a
  conversation's message far past anything the runtime was sized for.

The helper lives in the Runs module beside its two callers. It is not a Contracts type: nothing
outside Runs assembles agent input.

## D4 — What a trial does *not* reproduce, said out loud

A scratchpad attempt is a Run's instruction minus two things, and both are named in the surface's
copy rather than left for somebody to discover from a divergent result:

- **The approval-gate framing.** An Automation with `requiresApproval` runs its prompt in a planning
  phase, with a sentence appended telling the agent a human will review what it produces, and a later
  phase that receives the approved plan. A scratchpad has no Automation and therefore no gate; it
  reproduces the ungated shape.
- **The per-Automation timeout.** A timeout belongs to the Automation. A scratchpad attempt runs
  under whatever bound the conversation runtime imposes.

Stating these is the point. A fidelity claim with unstated exceptions is the kind of claim ADR-0009
exists to prevent, and this one is about the product's own behaviour rather than a citation.

## D5 — Where the spend appears afterwards

The issue asked whether a scratchpad attempt shows up anywhere once it is done, noting that "it
vanishes" is not obviously right given BR-011.

**Decided: it appears exactly where a conversation appears — on its own row, with its own spend, and
honest about whether that total is exact.** No new aggregation is introduced, because none exists for
conversations either. Building a project-level rollup for scratchpad attempts alone would say the
money spent on a draft matters more than the money spent on a conversation, which nobody believes.

If a project-level cost view is wanted later, it is one item covering conversations and scratchpad
attempts together, since by D1 they are the same rows.

## D6 — The message bound, measured

`SayValidator` caps a message at 10,000 characters. Measured against real material of exactly the
kind a scratchpad is for:

| Source | Largest prompt |
| --- | --- |
| the sibling repository's `ds/sync.md` | 9,741 chars |
| this repository's `aio/sync.md` | 8,020 chars |

The largest real prompt sits at **97%** of the current cap. Reusing it would refuse the prompts this
product exists to author, and would do so as a validation error on somebody's paste.

**Decided: 40,000 characters** — roughly four times the largest observed, so headroom that does not
need revisiting every time a prompt grows a section, and still a bound rather than an invitation. It
applies to every conversation message, not only scratchpad attempts: one rule, since a scratchpad
attempt *is* a message and a per-caller bound would have to be justified by a difference that does
not exist.
