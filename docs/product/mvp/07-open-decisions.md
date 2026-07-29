# Open decisions

Decisions **not yet made**. Each names what it blocks. The Definition of Ready forbids
proposing work that depends on one of these — a decision-closure task goes first
([RULE-006](08-backlog-shaping-rules.md)). Closed entries move to
[10-locked-mvp-decisions.md](10-locked-mvp-decisions.md); never edit in place.

- **OPN-002 — Entra ID reality check.** *(Carried from the charter.)* Unverified:
  (a) app registrations can be created in the Plain Concepts tenant for this project;
  (b) a workable local-dev + functional-test auth strategy exists (Entra cannot be
  containerized). **Blocks:** the auth foundation slice, UC-001, and Phase 5's smoke
  E2E. **Closes:** owner exercises both paths for real before the auth slice is
  proposed. Reopen trigger on DEC-024 if verification fails (candidates: GitHub OAuth,
  Keycloak).

- **OPN-005 — how a human converses live with an agent.** *(Recorded and closed together
  by #149; the entry is kept so the question and its blocking scope stay readable.)*
  Unmade: whether a live discussion keeps a process alive, pays a pass per message, or
  does both depending on presence. **Blocked:** any live-discussion capability, and any
  portal→agent ingest surface (DEC-050 deferred that direction). **Closed:** by
  [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md) and
  [DEC-055](10-locked-mvp-decisions.md) — a pass per message, because BR-006's untimed
  human wait and a paid idle process cannot both be honoured.

**Closed:** OPN-001, OPN-003, OPN-004, OPN-005 — see [10-locked-mvp-decisions.md](10-locked-mvp-decisions.md).
