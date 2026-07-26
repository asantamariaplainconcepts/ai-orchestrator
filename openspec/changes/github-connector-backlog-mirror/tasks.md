# Tasks — github-connector-backlog-mirror

The module and its seams first, then the vendor implementation, then the loop, then the screen.
The guardrails are checked at the point the second module exists, because that is the first moment
they can actually be wrong.

## 1. The secret seam (BuildingBlocks)

- [ ] 1.1 `ISecretResolver` in `AiOrchestrator.BuildingBlocks`: resolve a secret **by name, per
      read**, failing loudly when it is missing — never empty, never a default, never cached in a
      way that survives a rotation.
- [ ] 1.2 A configuration-backed implementation for development (user-secrets / environment),
      registered **in the host's composition root** — not in a module. `IModule.Add` only receives
      `IServiceCollection`/`IConfiguration`, so it structurally cannot call an Aspire client
      integration; the host is the only place with `IHostApplicationBuilder` (design D3).
- [ ] 1.3 The Key Vault implementation is **not written here** — it belongs to #8, which is where a
      real vault exists. Leave the seam and a note; do not add
      `Aspire.Hosting.Azure.KeyVault` / `Aspire.Azure.Security.KeyVault` to CPM in this change,
      since an unused package is a claim we have not exercised.
- [ ] 1.4 Verify: resolving a missing name throws with the name in the message; a secret added
      **after** startup resolves without a restart; nothing in the application reads a credential
      from configuration directly; no module references a cloud SDK.

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

- [ ] 6.0 **Route this work through the `aio-design` skill**: read `DESIGN.md` first, compose kit
      components, resolve copy through the catalogue, run the validator before pushing.
- [ ] 6.1 A project route showing the Connector configuration form and the mirrored Stories, built
      **only from existing kit classes** (design D8 maps each need to one): shell for layout,
      `.card`/`.field`/`.input`/`.btn` for the form, `.list`/`.mono` for Stories, `.badge-*` for
      state and labels, `.state` and `.state-error` for the two *different* empty states from 5.4,
      `.empty-value` for absent fields.
- [ ] 6.2 If the page genuinely needs something the kit lacks, **add it to
      `docs/design-system/ui-kit/` and regenerate** — never inline a style in the screen. A
      component invented in a feature is how a second source of truth begins.
- [ ] 6.3 All copy through the typed i18n catalogue, following the content fundamentals: sentence
      case, verb-first buttons, the documented empty/error patterns, the locked vocabulary
      (Story, Connector), relative timestamps for recency.
- [ ] 6.4 Verify: `pnpm lint`, `pnpm typecheck` and
      `bash .claude/skills/aio-design/scripts/validate-design-system.sh` all pass; the page is
      checked in **both themes** with keyboard focus visible on every interactive element; the
      "no Stories" and "last poll failed" states are visually distinct.

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
