# Tasks: prompt-scratchpad

## 1. One description of a Story (design D3)

- [x] 1.1 Extract the Run's Story framing — number, title, state, labels, bounded description — into
      one helper in the Runs module, beside its two callers. Not a Contracts type: nothing outside
      Runs assembles agent input. → `Domain/StoryDescription.cs`.
- [x] 1.2 `RunExecutor` uses it, producing identical instructions to today.
- [x] 1.3 `HoldConversation.SayHandler` uses it, replacing `$"{Title}\n\n{Body}"`. A conversation's
      Story context gains state and labels and becomes bounded.
- [x] 1.4 A functional test asserts both paths describe the same Story identically —
      `AStory_Should_BeFramedAsARunFramesIt` runs the conversation *and* a Run over the same Story
      and asserts the Run's instruction contains the conversation's context verbatim, so a helper
      one caller quietly bypassed would not pass.

## 2. The message bound (design D6)

- [x] 2.1 `SayValidator`'s maximum raised from 10,000 to 40,000, with the measurement in the comment:
      the largest real prompt observed is 9,741 characters, so the old bound refused what the
      product exists to author.
- [x] 2.2 A functional test asserts both edges — 9,741 and 40,000 accepted, 40,001 refused.
      **The realistic length alone was not enough:** the first version of this test asserted only
      9,741 accepted, which the *old* 10,000 cap also satisfied, so it stayed green under the
      reverted bound. Caught by the mutation check in 4.6 and rewritten.

## 3. The scratchpad surface (design D1, D4)

- [x] 3.1 `features/automations/PromptScratchpad.tsx`, on the Automations tab beside the prompt-path
      field.
- [x] 3.2 A multi-line prompt input, an optional Story subject, and a run control. Each run starts a
      **new** conversation and sends one message (D1), so an edited draft is tried afresh.
- [x] 3.3 The reply, its cost, and unknown-not-zero rendered with the shared `formatCost`.
- [x] 3.4 A failure is shown and the panel stays usable, taking another attempt.
- [x] 3.5 Copy in `shared/i18n`: the text is not saved; commit it to the project's prompts directory;
      and D4's two fidelity limits — no approval-gate planning phase, no per-Automation timeout.
- [x] 3.6 Design-system gate passes. One kit addition was needed: `shared/ui/textarea.tsx`, because
      the kit had no multi-line input and a bare `<textarea>` would have been the drift the contract
      exists to stop.

## 4. Tests

- [x] 4.1 Functional: an attempt runs one pass with the repository named and the reply readable, and
      creates no Run — asserted by querying the Runs table.
- [x] 4.2 Functional: an Automation on a Story with an attempt in flight still matches and runs.
- [x] 4.3 Functional: after an attempt, a Run resolves its prompt from the repository — asserted on
      the instruction the agent received, which is the only place a second source could appear.
- [x] 4.4 **Not written, and deliberately.** #189's permission criteria are satisfied by *not adding
      anything*: the scratchpad introduces no request, so the refusal is `HoldConversation`'s, which
      `ProjectRoles_Should_Constraint` already polices structurally (an undeclared request fails
      closed; a declared permission nobody grants is a red build) and `ProjectRoleAssignment_Should_
      Constraint` already exercises behaviourally against the same pipeline. A duplicate assertion in
      the Runs suite would need a second-caller harness that suite does not have, to re-prove a
      guarantee enforced centrally.
- [x] 4.5 E2E: the scratchpad is reachable from the Automations tab, the prompt field is a textarea,
      and the two sentences only a browser can check are on screen. **It does not assert a reply** —
      this habitat composes the in-process runtime, so an attempt would clone a repository and call a
      model, which CI has neither the credentials nor the minutes for. The same limit
      `AskTheAgent_Should_Constraint` states for the conversation it is built on.
- [x] 4.6 Mutation-checked (ADR-0004), with the build confirmed at zero errors before believing any
      red: reverting `StoryDescription.Of` to the old title-and-body framing reddens 1.4, and
      reverting the bound to 10,000 reddens 2.2 — the second only after 2.2 was rewritten, which is
      how the false-green was found.

## 5. Documentation

- [x] 5.1 `ARCHITECTURE.md`: the scratchpad is a conversation, each attempt fresh, and a Story is
      described in exactly one way — stated in the executor section beside DEC-062, not in a new one.
- [x] 5.2 No new decision record. D1 and D5 are applications of #166 and BR-011 rather than
      revisions of them; D3 and D6 are implementation judgements whose evidence is in `design.md`.
