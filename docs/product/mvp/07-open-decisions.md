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

- **OPN-006 — whether a self-host backlog read can use the host's own credentials.**
  *(Recorded and closed together by #223; the entry is kept so the question and its
  blocking scope stay readable.)* Unmade: whether a self-host deployment may reach the
  vendor as the machine — through the host's own tooling — rather than as a credential
  the operator supplied, given that a Local Run already runs git with the host's
  credentials and an agent in local mode already reaches the vendor as whatever the
  machine is logged in as (`AgentCredentialEnvironment.For`). **Blocked:** hiding the
  credential in self-host, and any host-derived vendor authentication — concretely
  [#347](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/347), which
  needs vendor writes as well as reads. **Closed:** by
  [ADR-0028](../../adr/0028-a-self-host-connector-may-authenticate-as-its-host-a-deployment-may-not.md)
  and [DEC-069](10-locked-mvp-decisions.md) — permitted in self-host through the machine's
  git credential helper, refused in a governed deployment, because a deployment has no host
  identity to borrow and the machine is not the operator's (the same asymmetry
  [ADR-0021](../../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
  drew, on the same ground).

- **OPN-008 — whether a terminal may open outside a sandbox on a machine its operator owns.**
  *(Recorded and closed together by #357; the entry is kept so the question and its blocking scope
  stay readable.)* Unmade: whether `run.attach` may yield a shell on the **host** where no sandbox
  exists. Today the grant is bounded by construction — a shell inside a per-Run sbx microVM that dies
  with the Run — and `AgentSandboxComposition` registers the terminal only in the sbx branch, so the
  one habitat [ADR-0021](../../adr/0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md)
  permits attaching in is the one habitat with no terminal. DEC-065 does not settle it: it was decided
  about a session *inside a sandbox*, and its companion requirement presumes one exists. **Blocked:**
  any terminal in the default local habitat, and the sandbox-shaped names the seam carries. **Closed:**
  by [DEC-070](10-locked-mvp-decisions.md) — a bounded host terminal in self-host, refused in a
  deployment ([ADR-0029](../../adr/0029-a-terminal-may-open-on-the-host-bounded-to-the-runs-own-checkout.md)).

**Closed:** OPN-001, OPN-002, OPN-003, OPN-004, OPN-005, OPN-006, OPN-007, OPN-008 — see [10-locked-mvp-decisions.md](10-locked-mvp-decisions.md).

**None remain open.** *(This file asserted that once before while #223 was open — corrected
by #301, true again once #223 closed OPN-006, and true again now that #357 has closed OPN-008.)*
