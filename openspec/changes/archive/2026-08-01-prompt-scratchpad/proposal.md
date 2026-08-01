# Proposal: prompt-scratchpad

## Why

[#189](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/189). After
[#162](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/162) an Automation *is* a
prompt in the repository. The catalogue is one action, and what a Run does is decided entirely by a
file the project wrote. That makes prompt-writing the main configuration activity — and there is no
way to find out what a prompt does short of committing it, wiring an Automation to it, applying a
trigger label, and waiting for a Run.

A **scratchpad** closes that loop. An Admin pastes prompt text in the portal, optionally names a
Story, runs it once against the project's repository, and reads the reply and the cost. Then they
commit the file themselves, if it did what they wanted.

**The repository stays the only place a prompt lives.** Nothing here is stored on an Automation and
nothing here is read at Run time (#150, #162). A scratchpad is a way to *try* text, not a second home
for it.

## What changes

- **A scratchpad is a conversation** (#166), started fresh for each attempt, whose message is the
  prompt text. Not a sibling capability: nine of this issue's twelve acceptance criteria are already
  properties of a conversation's message pass — the repository cloned with the project credential,
  the Story's context from the mirror, cost recorded with unknown-never-zero, a failure that leaves
  the surface usable, refusal for a caller with no role, a Member may use it, no Run, no cap slot, no
  lock. See design **D1**.
- **A Story is described to an agent in one way, shared by both paths.** Today a Run frames a Story
  with its number, title, state, labels and a truncated description; a conversation frames it as
  title and body only, unbounded. A scratchpad built on the second would be trying the prompt against
  a *different* input than the Run will give it, which defeats the point. Both paths move to one
  shared description (design **D3**) — the Run's, which is the one that must be faithful.
- **The message cap rises from 10,000 to 40,000 characters.** Measured, not guessed: the largest real
  prompt in the sibling repository this product is modelled on is **9,741** characters and the largest
  in this one is **8,020**. A scratchpad reusing today's cap would refuse the very prompts the product
  exists to author. 40,000 is roughly four times the largest observed, and still bounded.
- **A portal surface on the Automations tab**, beside the prompt-path field — which is where prompt
  writing now happens. It says plainly that the text is not saved, and names where to put it when it
  is right: the project's prompts directory.

## What this revises

Nothing. No decision is overturned. Two clarifications are recorded rather than left implicit:

- **DEC-026 / #162's premise is reinforced, not weakened.** A scratchpad is the one shape of
  "try a prompt in the portal" that does not create a second source for what an Automation does: the
  text is never persisted anywhere the Run path reads, and the Run resolves its prompt from the
  repository exactly as before.
- **#166's conversation gains a better Story description** as a side effect of D3 — state, labels and
  a bounded description, where it previously had title and body unbounded. That is a strict
  improvement to an existing capability, and it is stated here rather than smuggled in.

## What does not change

- BR-001 and BR-002. Nothing here is a Run: no cap slot, no Story lock, no dispatch. An Automation on
  a Story with a scratchpad attempt in flight matches and runs as usual.
- BR-008. The mirror stays read-only; a scratchpad writes nothing at the vendor.
- BR-010. Credentials by name only, both sides of the seam.
- BR-011. Unknown is not zero, on a scratchpad attempt as on any other pass.
- The Run path. It resolves its prompt from the repository, unchanged.
- No new permission. `RunPermissions.HoldConversation` already answers "a caller with no role is
  refused; a Member may use it", and inventing a second permission for the same spend would be an
  inconsistency with no argument behind it (design **D2**).

## Impact

- **Modules:** Runs only. One shared Story-description helper, one validator bound raised, one
  frontend panel. No new aggregate, no new table, no migration.
- **Fidelity limits, stated rather than discovered** (design **D4**): a scratchpad attempt reproduces
  a Run's instruction *except* the approval-gate framing (an approval-gated Automation runs its
  prompt in a planning phase) and the per-Automation timeout, which belongs to the Automation a
  scratchpad does not have. Both are named in the surface's copy.
- **Cost:** every attempt spends one agent pass, exactly as a conversation message does, and it is
  recorded on the row that spent it (design **D5**).
