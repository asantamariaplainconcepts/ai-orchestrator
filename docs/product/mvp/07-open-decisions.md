# Open decisions

Decisions **not yet made**. Each names what it blocks. The Definition of Ready forbids
proposing work that depends on one of these — a decision-closure task goes first
([RULE-006](08-backlog-shaping-rules.md)). Closed entries move to
[10-locked-mvp-decisions.md](10-locked-mvp-decisions.md); never edit in place.

- **OPN-002 — Entra ID reality check.** *(Recorded and closed by #11/#167; the entry is
  kept so the question and its blocking scope stay readable.)* Unverified was:
  (a) an app registration can be created in a tenant available to this project;
  (b) a workable local-dev + functional-test auth strategy exists (Entra cannot be
  containerized). **Blocked:** the auth foundation slice, UC-001, and Phase 5's smoke
  E2E. **Closed:** by [DEC-058](10-locked-mvp-decisions.md) — both halves exercised for
  real on 2026-07-30. (a) `infra/entra-app.sh` created the registration, its service
  principal and a vaulted client secret in the owner's test tenant, first try;
  (b) answered by the BFF shape: the server owns the session, so the test tiers keep
  injecting `ICurrentPrincipal` and Entra is composed only in the real host.

- **OPN-005 — how a human converses live with an agent.** *(Recorded and closed together
  by #149; the entry is kept so the question and its blocking scope stay readable.)*
  Unmade: whether a live discussion keeps a process alive, pays a pass per message, or
  does both depending on presence. **Blocked:** any live-discussion capability, and any
  portal→agent ingest surface (DEC-050 deferred that direction). **Closed:** by
  [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md) and
  [DEC-055](10-locked-mvp-decisions.md) — a pass per message, because BR-006's untimed
  human wait and a paid idle process cannot both be honoured.

**Closed:** OPN-001, OPN-002, OPN-003, OPN-004, OPN-005 — see [10-locked-mvp-decisions.md](10-locked-mvp-decisions.md).

**None remain open.**
