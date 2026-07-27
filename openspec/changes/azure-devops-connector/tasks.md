# Tasks — azure-devops-connector

## 1. The vendor

- [ ] 1.1 `BacklogVendor.AzureDevOps`; optional `CodeRepository` on Connector + migration; the
      configure slice accepts both (design D5).
- [ ] 1.2 `AzureDevOpsBacklogConnector`: every seam method, Azure types contained (D1/D4),
      errors translated into the existing taxonomy.
- [ ] 1.3 Process-dependent fields attempt-and-surface rather than assume (D3).

## 2. Tests

- [ ] 2.1 Unit tests over the translation: tag string ↔ label list, work item → VendorStory,
      estimate field selection, error translation.
- [ ] 2.2 The guardrail suite passes with two vendor implementations present.

## 3. Portal

- [ ] 3.1 Vendor choice and the optional code repository in the Connector form; catalog copy;
      lint + build.

## 4. Close-out

- [ ] 4.1 OPN-003 closed in docs/product/mvp/07 and locked in 10; ARCHITECTURE.md gains the
      second vendor **and its unexercised status** (design D2); CI's own filtered command;
      CI green.
