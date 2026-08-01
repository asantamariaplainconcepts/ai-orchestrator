## 1. Catalogue wiring

- [x] 1.1 Add the optional `automation` block (trigger, requiresApproval, outputLabels) to the
      portable tier's manifest entries and the manifest records in `StarterCatalogue`.
- [x] 1.2 Extend the manifest-enumeration test: every wiring names its own prompt, no duplicate
      normalised triggers in the catalogue.

## 2. The action

- [x] 2.1 `SetUpDefaultAutomations` use case in the Projects module:
      `POST /api/projects/{id}/automations/set-up-defaults`, Admin permission, creates missing
      wired Automations enabled, skips existing triggers case-insensitively, converges on
      uniqueness races.
- [x] 2.2 Report shape: created triggers, skipped triggers, missing prompt paths (read through
      `IDocumentReader.ReadPrompt`, never written).

## 3. Tests

- [x] 3.1 Functional: fresh project → full set created; pre-existing trigger (odd casing) →
      skipped and named; second invocation → all skipped; Member → refused; missing prompt files
      → named with their directory.
- [x] 3.2 Regression: existing automation suites unchanged.
