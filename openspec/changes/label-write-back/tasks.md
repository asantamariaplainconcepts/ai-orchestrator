# Tasks — label-write-back

## 1. The seam's first writes

- [ ] 1.1 `ApplyLabel`/`RemoveLabel` on `IBacklogConnector` (design D2); stub connector in the
      functional tiers mutates its story list.
- [ ] 1.2 GitHub implementation via Octokit with the existing error translation; remove-of-absent
      and apply-of-present are no-ops (design D3). Unit tests over the translation.

## 2. The endpoints

- [ ] 2.1 `PUT`/`DELETE .../stories/{vendorStoryId}/labels/{label}`: resolve connector + secret
      name, write back, then `BacklogSynchroniser.Synchronise` (design D1). Vendor errors map to
      the existing problem responses.
- [ ] 2.2 Functional tests: apply shows on the mirror and (with an enabled Automation) creates a
      Run through the real event path — the portal-probe scenario end-to-end; remove; vendor
      refusal leaves the mirror unchanged; idempotence both ways.

## 3. The portal surface

- [ ] 3.1 Backlog rows: apply affordance for enabled trigger labels missing from the Story,
      remove affordance for those present, read-only pills otherwise (design D4); catalog copy.
- [ ] 3.2 Frontend lint + build green; themes verified.

## 4. Close-out

- [ ] 4.1 Guardrails: no vendor SDK outside Connectors — suite green.
- [ ] 4.2 ARCHITECTURE.md touch-up (the seam now writes); full suite; CI green.
