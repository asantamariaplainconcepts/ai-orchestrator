# Tasks — delete-automation

## 1. The Runs contract

- [ ] 1.1 `AiOrchestrator.Modules.Runs.Contracts` with a usage reader; implementation in Runs;
      Projects references the Contracts assembly only (design D2).

## 2. The use case

- [ ] 2.1 `DeleteAutomation`: project-scoped resolve (D4), usage refusal naming the count (D1),
      otherwise remove.

## 3. Portal

- [ ] 3.1 Delete control beside the enable toggle; the refusal surfaced as its own message;
      copy; lint + build.

## 4. Tests

- [ ] 4.1 Functional: unused deletes; used refuses naming the count; the in-flight Run completes
      after a refusal; the trigger is reusable after deletion; a foreign id is not found.

## 5. Close-out

- [ ] 5.1 UC-006's text covers removal; guardrails green with the new Contracts edge; CI's
      filtered command locally; CI green.
