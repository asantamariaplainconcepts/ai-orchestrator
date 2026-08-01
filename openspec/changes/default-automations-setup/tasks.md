## 1. Catalogue wiring

- [ ] 1.1 Add the optional `automation` block (trigger, requiresApproval, outputLabels) to the
      portable tier's manifest entries and the manifest records in `StarterCatalogue`.
- [ ] 1.2 Extend the manifest-enumeration test: every wiring names its own prompt, no duplicate
      normalised triggers in the catalogue.

## 2. The action

- [ ] 2.1 `SetUpDefaultAutomations` use case in the Projects module:
      `POST /api/projects/{id}/automations/set-up-defaults`, Admin permission, creates missing
      wired Automations enabled, skips existing triggers case-insensitively, converges on
      uniqueness races.
- [ ] 2.2 Report shape: created triggers, skipped triggers, missing prompt paths (read through
      `IDocumentReader.ReadPrompt`, never written).

## 3. Tests

- [ ] 3.1 Functional: fresh project → full set created; pre-existing trigger (odd casing) →
      skipped and named; second invocation → all skipped; Member → refused; missing prompt files
      → named with their directory.
- [ ] 3.2 Regression: existing automation suites unchanged.
