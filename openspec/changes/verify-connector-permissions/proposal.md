# Proposal: verify-connector-permissions

## Why

Issue #132 (ACT-001 configures; UC-004). UC-004's promise is *"a Connector that exists is one that
works"*, enforced by calling `VerifyAccess` before anything is stored. But `VerifyAccess` probes
with `client.Repository.Get`, which succeeds on a fine-grained PAT holding nothing but the metadata
permission every token gets by default. The check passes, the Connector is stored as verified, and
the first thing the pipeline actually needs fails hours later somewhere else.

Observed on dev on 2026-07-29, with unusually clean evidence. Run
`019fac28-e725-7864-8f83-fbe7c5858a03` failed reading `docs/process/definition-of-ready.md` with
*"the API returned Forbidden"*, while the same Connector reported a fresh `lastSyncedAt` and no
failure — the identical token reads **issues** perfectly and is refused on repository **contents**.
Every conversational action reads a document from the repository, so that token could never run
grill, propose or sync, and the product had no way to say so until a Run burned its dispatch
discovering it.

The product also said the opposite: Connector health (#97) derives from polling alone, so it stayed
green all day while three of the four pipeline steps were unreachable.

## What changes

- **The probe covers the reads the pipeline performs** (design D1): listing Stories *and* reading a
  document, not the cheapest call that returns 200.
- **The probe answers per capability, not true/false** (design D2). That is what lets the refusal
  name which read failed, and what the on-demand test renders.
- **A refusal is told apart from an outage** (design D3): `GitHubBacklogConnector.Translate`
  currently sends every `ApiException` that is not `NotFoundException` or
  `RateLimitExceededException` — Octokit's `ForbiddenException` included — into
  `VendorUnavailable("the API returned {status}")`. "Could not be reached" is false whenever the
  vendor answered, and the vendor's own reason is discarded.
- **The Admin can run the probe on demand** from the Settings tab, against the stored credential,
  without re-entering a token (design D4).
- **One probe, two entry points** (design D5): saving and testing call the same code, or they drift
  and the button starts reassuring people about a check that no longer gates saving.

## Impact

- Specs: `connector-configuration` — one MODIFIED requirement (the credential is verified before
  the Connector is stored, which gains what "verified" means) and one ADDED (testing on demand).
- Code: `IBacklogConnector.VerifyAccess` returns a richer result; both vendor implementations;
  `Translate`'s Forbidden branch; `ConfigureConnector`'s refusal path; one new query slice for the
  on-demand test; the Settings panel.
- No schema change: nothing about the probe is persisted.

## Out of scope

- **Verifying write permissions.** A probe that writes leaves a label, a comment or a branch in
  somebody's repository, which the Admin did not consent to by pressing save. Write failures
  surface on their first Run, with the vendor's reason, which design D3 makes legible.
- **Reading the vendor's declared scopes.** GitHub exposes these unevenly between classic and
  fine-grained tokens, so a check built on them is reliable for one kind of credential and
  misleading for the other.
- **Connector health reflecting the probe.** The finding that it reports polling only is real and
  is its own slice — it touches the read model, the projects list, and the question of when a
  background re-probe should run.
- Re-verifying stored Connectors in the background, or migrating what already exists.
