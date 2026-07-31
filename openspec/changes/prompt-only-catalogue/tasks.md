# Tasks — prompt-only-catalogue

## The vocabulary

- [ ] 1.1 `AutomationAction` keeps one member: `RepositoryPrompt`. The other seven leave the enum,
      the API surface and the frontend's two lists.
- [ ] 1.2 `RubricPath` is **renamed** to `PromptPath`, not removed: #150 made it how a
      `RepositoryPrompt` names its prompt file, so removing it would break the one surviving action.
      "Rubric" was the grill's word (design D1a).
- [ ] 1.3 A migration that **deletes** Automations naming a removed action and **renames** the rubric
      column to the prompt path, keeping every value. Deleting rather than converting: there is no
      prompt to point a retired Automation at, and one that matches and then always fails is worse
      than one that is gone (design D2).

## The executor

- [ ] 2.1 The per-action branches go. The executor clones, resolves the prompt, runs the agent, and
      records the result.
- [ ] 2.2 Every post-hoc write goes with them: no publishing a pull request, no comment, no state
      transition, no estimate parsing, no reading the agent's output for something to publish.
- [ ] 2.3 The output label is still applied on success — the one carve-out, and it is machinery
      (design D3).
- [ ] 2.4 `requiresApproval` still routes two phases; the executor no longer promises phase one wrote
      nothing (design D4).

## The removals that follow

- [ ] 3.1 The one-click defaults, and the bulk label-ensure that served them.
- [ ] 3.2 The grill's readiness machinery and the propose/sync ceremonies.
- [ ] 3.3 Every test that asserted a retired ceremony, deleted rather than adapted — an adapted test
      for a removed behaviour asserts nothing and reads as coverage.
- [ ] 3.4 **Kept, dormant, and stated:** `AwaitingInput`, `ConversationGate`, `ResumeChecker`,
      `RunMarker` and the inbox's waiting-for-input category. Removing them is out of scope by the
      issue's own words, so the dormancy is written down instead (design D5).

## The decision

- [ ] 4.1 A DEC records the inversion, revising DEC-026, DEC-048 and #150's bounded-shell stance,
      naming the grants follow-up and stating both caveats — phase containment and cancellation are
      prompt-level promises now.
- [ ] 4.2 ARCHITECTURE.md's action section describes the new reality.

## Verification

- [ ] 5.1 Functional: an Automation naming a retired action is refused; one naming the prompt runs,
      cloning the workspace and resolving the prompt live.
- [ ] 5.2 Functional: a Run that succeeds writes nothing to the vendor except its output labels —
      the assertion that would catch a ceremony surviving the removal.
- [ ] 5.3 Functional: a missing prompt file fails naming the resolved path (#150, unchanged).
- [ ] 5.4 Migration: an Automation naming a retired action is gone afterwards, and its past Runs
      still render — exercised against a real database, not asserted.
- [ ] 5.5 E2E: the form offers one action, and a project's workflow still wires and runs.
- [ ] 6.1 CI green; evidence on #162.
