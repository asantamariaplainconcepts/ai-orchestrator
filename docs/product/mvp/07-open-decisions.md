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

- **OPN-007 — whether a human may take the keyboard in a Run's own agent session.**
  *(Recorded and closed together by #301; the entry is kept so the question and its
  blocking scope stay readable.)* Unmade: whether a human may attach to a live agent
  session at all, given that [ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md)
  refused one on premises that have since moved — DEC-013 superseded (#296), and
  "nothing idles" already revised by DEC-061 and DEC-063 — leaving BR-006 as the only
  pillar still standing. **Blocked:** any attached-session capability, and the terminal
  surface a Run's sandbox would need. **Closed:** by
  [ADR-0021](../../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md) —
  permitted in self-host, refused in a deployment, because ADR-0008's cost argument was
  always a deployment argument and a machine its operator owns is not one someone else
  pays for.

**Closed:** OPN-001, OPN-002, OPN-003, OPN-004, OPN-005, OPN-007 — see [10-locked-mvp-decisions.md](10-locked-mvp-decisions.md).

**Still open:** OPN-006 — whether a self-host backlog read can use the host's own
credentials — tracked in [#223](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/223)
and never written into this file. Recorded here so the count is honest; the entry itself
belongs to whichever change closes it. *(This file previously asserted "None remain open"
while #223 was open — corrected by #301.)*
