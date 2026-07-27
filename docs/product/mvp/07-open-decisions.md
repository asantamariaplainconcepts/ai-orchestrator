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

- **OPN-003 — Azure DevOps trigger semantics.** GitHub has labels; AzDO has *tags* and
  a different work-item/state model. The exact AzDO mapping for "trigger label", state
  transition targets, and the estimate field is undefined. **Blocks:** the AzDO
  Connector implementation (second per DEC-011 sequencing) — not the seam, which is
  designed from the normalized event model. **Closes:** during grill of the AzDO
  connector issue.

