# Tasks — automation-defaults

## 1. The seam

- [ ] 1.1 `EnsureLabel` on `IBacklogConnector`; GitHub creates-or-succeeds; Azure DevOps is an
      explicit no-op carrying the reason (design D3).
- [ ] 1.2 Reachable from the Projects module through the Backlog contracts surface, without
      either module referencing the other's implementation.

## 2. The default set and its use case

- [ ] 2.1 The set in code — four actions, labels, free runtime, approval on implement only
      (D1/D5).
- [ ] 2.2 Apply-defaults use case: creates what is absent, skips overlaps via BR-003, honours
      BR-002's cap, and returns created/skipped/label-outcome separately (D2/D4).

## 3. Portal

- [ ] 3.1 The action in the Automations section, its result surfaced (created, already present,
      labels not ensured); catalog copy; lint + build.

## 4. Tests

- [ ] 4.1 Functional: unconfigured project, repeat application, one trigger taken, no Connector,
      vendor refusal, cap reached.
- [ ] 4.2 The label reaches the vendor stub at repository level and no Story is touched.

## 5. Close-out

- [ ] 5.1 ARCHITECTURE.md notes the seam's first repository-level write and the Azure DevOps
      asymmetry; CI's own filtered command locally; CI green.
