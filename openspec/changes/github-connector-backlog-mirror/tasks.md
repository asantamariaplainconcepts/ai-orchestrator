# Tasks — github-connector-backlog-mirror

The module and its seams first, then the vendor implementation, then the loop, then the screen.
The guardrails are checked at the point the second module exists, because that is the first moment
they can actually be wrong.

## 1. The secret seam (BuildingBlocks)

- [ ] 1.1 `ISecretResolver` in `AiOrchestrator.BuildingBlocks`: resolve a secret by name, fail
      loudly when it is missing — never return empty or a default.
- [ ] 1.2 A configuration-backed implementation for development (user-secrets / environment),
      registered by default. The Key Vault implementation belongs to #8 and is not written here.
- [ ] 1.3 Verify: resolving a missing name throws with the name in the message; nothing in the
      application reads a secret from configuration directly.

## 2. The Backlog module

- [ ] 2.1 `AiOrchestrator.Modules.Backlog` with its own `backlog` schema, `DbContext`, migration
      and `ModuleBase`. `Connector` and `Story` are `internal sealed`, each holding a plain
      `ProjectId` — **no reference to the Projects assembly, no `.Contracts` project** (design D2).
- [ ] 2.2 **Verify the module seam for real, now that two modules exist:** the solution builds
      with analyzers attached to both; ArchTests pass with a genuine second module rather than a
      probe; the host discovers Backlog with **no edit to `AiOrchestrator.Server`** — that claim
      is in the architecture spec and has never been tested.
- [ ] 2.3 Verify: the Backlog migration touches only the `backlog` schema.

## 3. The connector seam and its GitHub implementation

- [ ] 3.1 `IBacklogConnector`: verify access, and fetch current Stories. Product vocabulary only —
      no Octokit type in the signature.
- [ ] 3.2 The GitHub implementation over Octokit 14: coordinate + credential verification with the
      two failure modes distinguished, Story retrieval with pagination, ETag conditional requests.
- [ ] 3.3 Verify: a deliberate bad-credential and a deliberate bad-repository case each produce
      their own distinct failure, not one generic error.

## 4. Configuring a Connector (UC-004)

- [ ] 4.1 `ConfigureConnector` slice: validator, handler, `ErrorOr` domain errors in a
      `{Entity}Errors` type, verification-before-store, upsert semantics (one Connector per
      Project).
- [ ] 4.2 `GetConnector` slice returning coordinates and secret name — never a token.
- [ ] 4.3 Verify: the stored row contains no token; the API response contains no token; a rejected
      credential and an unknown repository return different problems.

## 5. Polling and the mirror (UC-009)

- [ ] 5.1 Reconciliation: upsert by vendor id, mark absent what the vendor no longer returns,
      identity independent of title.
- [ ] 5.2 `RefreshBacklog` slice — the explicit, deterministic path the tests drive.
- [ ] 5.3 The hosted-service poller on the per-project interval (default 60s). It must not delay
      startup, must tolerate a Connector vanishing mid-loop, and **must not run inside the
      functional test host** — a background loop firing during tests is a flake generator.
- [ ] 5.4 Failure handling: record the failure and its reason against the Connector; leave the
      previous mirror readable; make "no Stories" and "last poll failed" distinguishable.
- [ ] 5.5 Verify: two consecutive polls with nothing changed leave the mirror byte-identical;
      a poll against an unreachable vendor leaves prior Stories readable and records the failure.

## 6. The project page (UC-007)

- [ ] 6.1 A project route showing the Connector configuration form and the mirrored Stories, built
      from the design-system kit, with empty, loading and error states — and the two *different*
      empty states from 5.4 rendered differently.
- [ ] 6.2 All copy through the typed i18n catalogue, following the content fundamentals; vendor
      ids in `.mono`.
- [ ] 6.3 Verify: `pnpm lint` and the design validator pass; the page is checked in both themes
      with keyboard focus visible.

## 7. Tests

- [ ] 7.1 Unit: reconciliation logic (add / update / disappear / rename), and the failure-mode
      mapping.
- [ ] 7.2 Functional: configure → poll → read, against real containers, with the vendor stubbed at
      the `IBacklogConnector` seam so the tier stays hermetic and does not depend on GitHub.
- [ ] 7.3 A concurrency test, per ADR-0002 — parallel refreshes of the same Project must not
      duplicate Stories.
- [ ] 7.4 E2E: the project page shows a mirrored backlog. Whether the vendor is stubbed at the
      HTTP boundary or a fixture repository is used is an implementation decision — but the E2E
      lane MUST NOT depend on a live GitHub token in CI.

## 8. Close-out

- [ ] 8.1 `ARCHITECTURE.md` and the module `context.md` updated: two modules now, and *why*
      Backlog owns the Connector (design D2) so the next reader does not "fix" it.
- [ ] 8.2 Record the referential-integrity debt from D2 where project deletion will have to deal
      with it.
- [ ] 8.3 Full verify sweep; CI green including E2E.
