# Proposal: portal-conversation

## Why

[#166](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/166), the capability
[ADR-0008](../../../docs/adr/0008-a-live-conversation-costs-a-pass-per-message.md) deferred to its own
item. Its prerequisite — the Connector comment write — has landed, and #162's abandonment left
`ConversationGate`, `ResumeChecker` and `RunMarker` standing with no producer. This is what they are
for.

Today the only way to talk to an agent is to comment on a Story and wait for a Run to resume. A Member
who wants to ask *"why did this fail"* or *"what would you do here"* has nowhere to ask.

The owner's decision, taken with the alternatives on the table: a conversation is **not a Run**, does
**not block**, and its subject is optional. That keeps BR-001 and BR-014 untouched — waiting blocks a
Story precisely because a Run occupies it, and a conversation that occupied one would stop every
Automation on that Story for as long as somebody kept talking, with BR-006 putting no limit on how
long that is.

## What changes

- A **Conversation** aggregate in the Runs module: a project, optionally a Story as its subject, and
  an ordered list of messages. No Run, no cap slot, no lock on the Story.
- Sending a message costs **exactly one agent pass** (ADR-0008). The pass's usage and cost are
  recorded against the conversation, and a pass whose usage the runtime did not report reads
  **unknown**, never zero (BR-011).
- The pass runs in an **on-demand session container per conversation**, warm while the conversation
  lives and stopped by the platform after inactivity (design D2). One conversation, one container, one
  project's PAT: isolation coincides with the credential boundary (DEC-030), and the portal never
  holds a project credential.
- A portal surface: start a conversation, send, read, and see what it has cost.
- A failed pass shows the failure and leaves the conversation open — a failed message is not a failed
  conversation.

## What this revises

**ADR-0008 said "nothing idles".** A container that stays warm for the length of a conversation does
idle, between one message and the next. Choosing that shape is a deliberate revision, recorded as a
new decision rather than left as a contradiction: the cost of a cold start per message is a
ten-second pause on every reply, which is the difference between a conversation and a ticket queue.
The platform's own inactivity timeout is what bounds the idling.

**The issue's exposure note is out of date, in the useful direction.** It reasoned that a conversation
surface "lets anyone reachable spend money" because the portal authenticated nobody. It does now
(#12), and permission is a function of caller and project (#13) — so this surface declares a
permission like every other operation, and the note's argument is spent rather than merely weakened.

## What does not change

- BR-001 and BR-002. A conversation is not a Run, occupies no cap slot, and blocks nothing. That
  **also means nothing caps concurrent conversations**, which the issue names as a known risk and
  leaves as its own decision; this change does not quietly invent a cap.
- The Run model, matching, and every existing dispatch path.
- Notifying anybody that a reply arrived.

## Impact

- **New deployable artifact:** the session container image, built and pushed like the dispatch job's.
- **Infrastructure:** a Container Apps **session pool**, which `azurerm` 4.81 does not model — verified
  against the provider's own schema, not assumed — so the stack gains the `azapi` provider for this
  one resource. The owner applies it; CI can prove everything up to it.
- **Data:** two new tables in the Runs schema, and their migration.
- **Specs:** `run-orchestration` gains the conversation; nothing existing is contradicted.
