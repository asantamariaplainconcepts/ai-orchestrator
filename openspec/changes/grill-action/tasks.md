# Tasks — grill-action

## 1. Configuration

- [x] 1.1 `AutomationAction.GrillToReady`; nullable `RubricPath`/`ReadyLabel` + migration;
      create/edit accept and validate them; `AutomationDetail` carries them.

## 2. Contracts

- [x] 2.1 `ApplyLabel` on `IStoryWriter`; a default-branch document read (`IDocumentReader`);
      both thin over existing seam methods.

## 3. The action

- [x] 3.1 Executor: rubric read (fail-first, design D2), conversation from the Run's birth
      (D3), first-line verdict contract (D1); READY → label + verdict + succeed; otherwise →
      AskAndWait.
- [x] 3.2 ARCHITECTURE.md: #78's "unconsumed" notice replaced by the grill as first consumer.

## 4. Portal

- [x] 4.1 Action in the catalogue lists; conditional rubric/label fields; copy; lint + build.

## 5. Tests

- [x] 5.1 Functional: gaps→questions+wait; resumed pass→ready label+verdict; already-ready
      first pass; missing rubric fails untouched; chain to a second Automation via refresh.

## 6. Close-out

- [x] 6.1 DEC-048 records the catalogue revision; UC-024 added to the corpus; CI's filtered
      command locally; CI green.
