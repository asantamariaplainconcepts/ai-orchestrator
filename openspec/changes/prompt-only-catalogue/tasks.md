# Tasks — prompt-only-catalogue

## The vocabulary

- [ ] 1.1 `AutomationAction` keeps only `RepositoryPrompt`; every other member is removed and the
      validators refuse anything else (design D2).
- [ ] 1.2 One migration: delete Automations naming a removed action. `OutputLabel` **stays** — it is the
      workflow, not the action's ceremony (design D3, D4).

## The executor

- [ ] 2.1 `RunExecutor` loses the action branch entirely: one path — resolve the prompt, clone the
      workspace with the project credential, run the agent holding it (design D2).
- [ ] 2.2 The publish ceremony, the comment/state/estimate write switch, the grill's rubric
      conversation, and the propose and sync procedures are removed.
- [ ] 2.3 `HandOn` **stays** and remains the one output-label write path — a test asserts it still fires
      on success, because it is the piece a literal reading of the issue would have deleted (design D3).
- [ ] 2.4 What stays, stays: two-phase routing (BR-007), the phase budget (BR-005), the cancellation
      boundary, log streaming, the usage record, terminal states.
- [ ] 2.5 `AwaitingInput` and its resume path are left in place, unreachable and commented as such
      (design D6).

## The seeded defaults

- [ ] 3.1 The one-click defaults and the label-ensuring step are removed with the catalogue.

## The portal

- [ ] 4.1 The form offers the prompt action only, with its file-name field; the output-label field stays.
- [ ] 4.2 The canvas, the human-review block and the board's chain ordering are **untouched** — verified
      still working, since they were the collateral a literal reading would have taken (design D3).
- [ ] 4.3 i18n keys for the removed actions and the defaults are deleted; the mock stops serving
      defaults.

## The record

- [ ] 5.1 A DEC records the inversion — revising DEC-026, DEC-048 and DEC-057 — naming the two promises
      that become prompt-level (design D5) and the grants model as the follow-up.
- [ ] 5.2 ARCHITECTURE.md's action section describes one action.

## Verification

- [ ] 6.1 Tests for removed behaviour are deleted, not weakened; 31 test files name a removed action and
      each is either deleted or reduced to what still holds.
- [ ] 6.2 A test asserting the orchestrator publishes nothing of the action's own after the agent — no
      pull request, no comment, no state, no estimate — while the output label still lands. That pair is
      the claim this whole change rests on.
- [ ] 6.3 A test that any action but the prompt is refused, at create and at execute.
- [ ] 6.4 The migration is verified non-empty and its delete is exercised against a seeded row.
- [ ] 6.5 E2E: an Automation is created with a prompt name and runs; the canvas and the human block are
      still on the tab and still work; the board still moves a card.
- [ ] 7.1 CI green; evidence on #162, including what was deleted and what was deliberately left
      unreachable.
