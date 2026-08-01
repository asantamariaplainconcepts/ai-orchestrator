# Tasks: prompt-scratchpad

## 1. One description of a Story (design D3)

- [ ] 1.1 Extract the Run's Story framing — number, title, state, labels, bounded description — into
      one helper in the Runs module, beside its two callers. Not a Contracts type: nothing outside
      Runs assembles agent input.
- [ ] 1.2 `RunExecutor` uses it, producing byte-identical instructions to today. A test pins the
      framing so a future edit to it has to be deliberate.
- [ ] 1.3 `HoldConversation.SayHandler` uses it, replacing `$"{Title}\n\n{Body}"`. A conversation's
      Story context gains state and labels and becomes bounded.
- [ ] 1.4 A functional test asserts both paths describe the same Story identically — the spec
      scenario, asserted against the two real call sites rather than the helper alone.

## 2. The message bound (design D6)

- [ ] 2.1 Raise `SayValidator`'s maximum from 10,000 to 40,000, with the measurement in a comment:
      the largest real prompt observed is 9,741 characters, so the old bound refused what the product
      exists to author.
- [ ] 2.2 A functional test sends a message of ~10,000 characters (accepted) and one beyond 40,000
      (refused), so the bound is asserted at both ends rather than assumed.

## 3. The scratchpad surface (design D1, D4)

- [ ] 3.1 A `PromptScratchpad` panel in `src/frontend/features/automations/`, on the Automations tab
      beside the prompt-path field — where prompt writing now happens.
- [ ] 3.2 A multi-line prompt input, an optional Story subject, and a run control. Each run starts a
      **new** conversation and sends one message (D1), so an edited draft is tried afresh.
- [ ] 3.3 The reply, its cost, and unknown-not-zero rendered by the same rules the conversation panel
      uses. Reuse `formatCost`; do not invent a second cost presentation.
- [ ] 3.4 A failure is shown and the panel stays usable, taking another attempt — the conversation's
      own rule.
- [ ] 3.5 Copy, in `shared/i18n`: the text is not saved; commit it to the project's prompts directory
      when it is right; and D4's two fidelity limits — no approval-gate planning phase, no
      per-Automation timeout.
- [ ] 3.6 Design-system gate passes; no raw hex, no ad-hoc spacing.

## 4. Tests

- [ ] 4.1 Functional: an attempt runs one pass with the repository cloned and the reply readable, and
      creates no Run — asserted by counting Runs, not by absence of a badge.
- [ ] 4.2 Functional: an Automation on a Story with an attempt in flight still matches and runs.
- [ ] 4.3 Functional: after an attempt, a Run resolves its prompt from the repository — the
      scratchpad text is not consulted. Assert on the instruction the agent received.
- [ ] 4.4 Functional: a caller with no role is refused, disclosing nothing; a Member is allowed.
- [ ] 4.5 E2E: the scratchpad is reachable from the Automations tab, accepts text, and shows a reply.
- [ ] 4.6 Every red asserted here is verified to have compiled first (ADR-0004): confirm the mutated
      build reached zero errors before believing any failure.

## 5. Documentation

- [ ] 5.1 `ARCHITECTURE.md`: the scratchpad is a conversation, and the Story description is shared —
      state it where the executor and the conversation are described, not in a new section.
- [ ] 5.2 No new decision record unless something in review overturns D1 or D5; D3 and D6 are
      implementation judgements with their evidence in `design.md`.
