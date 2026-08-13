## Context

`DEC-069` / [ADR-0028](../../../docs/adr/0028-a-self-host-connector-may-authenticate-as-its-host-a-deployment-may-not.md)
closed OPN-006: a self-host Connector MAY authenticate as its host through the machine's **git
credential helper**, for reads and writes; a governed deployment MAY NOT.
[#348](https://github.com/asantamariaplainconcepts/ai-orchestrator/pull/348) wrote those obligations
into `connector-configuration` as requirements — *"a credential path is offered only where it can
succeed"*, lines 558-640 — and shipped **nothing executable**. Today no code reads a host credential,
and `ConfigureConnector` still requires one of a token or a secret name.

The product already delegates to the host's identity twice, and says so as a rule:
`AgentCredentialEnvironment.For` exports no vendor token when the host supplies none, because *"an
exported empty variable SHADOWS whatever auth the host's own tooling holds"*; and
`local-code-source` excuses a local-folder project from the code capabilities because *"git runs with
the host's credentials"*. This change extends that to the Connector's own reads and writes.

**The central factual bet was exercised, not assumed** (ADR-0006). On the development host,
`git credential fill` with `GIT_TERMINAL_PROMPT=0` answers for **both** vendors —
`github.com` and `dev.azure.com` — returning a username and a password, through the `osxkeychain`
helper. No value was read into any log or artifact to establish that.

## Goals / Non-Goals

**Goals:**

- An Admin in self-host adds a Project by naming a folder, and has a working Project: right vendor,
  Stories visible, a Local Run able to work in its own checkout (BR-016), no secret minted or stored.
- The three obligations ADR-0028 hands this change are executable and asserted: non-interactive
  resolution, the credential-source report, the honest permission statement.
- A governed deployment is bit-for-bit unchanged.

**Non-Goals:**

- A folder containing many repositories (one Project, one Connector, one set of coordinates — DEC-005).
- Any change to the three execution modes, to dispatch, or to BR-016's checkout rules.
- Any new host-inspection HTTP surface. Deliberately excluded rather than deferred.
- Cloning from a URL, or creating a Project from an empty folder.
- Removing the token from a self-host Connector that already stores one — a migration this change
  does not perform (see Risks).
- Behaviour when a host credential **expires mid-Run**. `DEC-069` accepts that as a stated cost; it
  is not a requirement of this slice, and inventing a policy for it here would be guessing. Named as
  a follow-up instead.

## Decisions

### D1 — One credential seam, two sources behind it

`ISecretResolver` resolves **by name**; a host credential has no name, so the host path cannot be a
second `ISecretResolver`. A reserved-name convention (`host:github.com`) was rejected as
stringly-typed and unenforceable.

Instead a Connector's credential becomes a small reference type resolved by one seam,
`IConnectorCredentialResolver`, which returns the value **and** its `CredentialSource`. It delegates
to `ISecretResolver` for a named secret and to a new `IHostCredentialResolver` for the host path.
This is what ADR-0028 anticipated — *"a host-derived credential is another resolver behind that seam,
not a change to the Connector's fourteen signatures"* — and keeps BR-010's *"one abstraction, per
read"* literally true. The fourteen `IBacklogConnector` methods keep `string token`: resolution
happens before them.

### D2 — Non-interactive is enforced by construction, not by hope

`IHostCredentialResolver` shells `git credential fill` with `GIT_TERMINAL_PROMPT=0`, a cleared
`GIT_ASKPASS`/`SSH_ASKPASS`, and a bounded timeout. Any of the three failing means the helper wanted a
human: it fails carrying that reason rather than waiting, and never substitutes an empty or default
credential. This is what stops a polling cycle stalling on a prompt (UC-009).

### D3 — The helper's password is the token; its username is for the record only

The helper answers `username` + `password`. Both vendors accept the password where the product
already puts a token — GitHub as a bearer, Azure DevOps as the basic-auth password the connector
already composes. So no connector method changes shape. The username is carried into the
`CredentialSource` report, never into an authorization header decision. Rejected: teaching both
connectors a second auth mode, which would change fourteen signatures to gain nothing.

### D4 — Verify before writing anything, then compensate rather than span a transaction

`CreateProject` (Projects module) cannot write a Connector directly (MOD001-005); it calls a new
`IConnectorWriter` on `Backlog.Contracts`, the `IStoryWriter` / `IPromptDirectoryWriter` shape. The two
modules own different schemas and different `DbContext`s, so one transaction cannot span them without
a shared `DbConnection` — a mechanism this repository uses nowhere, and introducing it for one flow
was rejected as too much new machinery for the failure it prevents.

Ordering carries the weight instead: **inspect → derive → verify live → create Project → write
Connector.** Every failure a user can cause (bad folder, unparseable remote, rejected credential)
happens before the first write. Should the Connector write still fail, the handler compensates by
removing the Project it just created — safe precisely because nothing else can reference a Project
created moments earlier in the same handler — and reports the failure. The alternative, leaving a
Project with no Connector, is the state the issue exists to abolish.

### D5 — Posture gating comes from the deployment capabilities read

The folder step is composed only in the self-host posture, the same discriminator `local-code-source`
already uses, and the portal decides from `GetDeploymentCapabilities` — never by re-deriving a posture
on the client. A create request carrying a folder in a governed deployment is refused, not ignored:
silence would let a client believe it configured something.

### D6 — The permission statement degrades honestly

`connector-configuration` already requires that a host-resolved credential is *not* described as
verified-by-derivation. The form states what **this configuration requires** in the vendor's
vocabulary, and says the product cannot determine what the host's credential holds. Implementation
follows the requirement; no new wording is invented here.

### UI governance

`docs/design-system/` and the derived `DESIGN.md` govern; kit primitives and Platform tokens only,
every string through the typed i18n catalogue (DEC-051, DEC-009, DEC-021). No new component is
introduced — the folder input is the existing text input with its explanation beside it, the rule
`connector-configuration` already sets for the Connector form.

## Risks / Trade-offs

- **A write capability cannot be verified without acting** → reported *not verifiable* carrying its
  reason, and saving proceeds. This is `connector-configuration`'s existing escape hatch, not a new
  concession; the cost is that a self-host operator can save a Connector whose write fails later
  inside a Run. Accepted explicitly by `DEC-069`.
- **A helper credential can expire mid-Run** → out of scope here (Non-Goals) and named as a follow-up.
  Nothing in this change makes it worse: today no host credential is used at all.
- **Two credential sources behind one resolver, permanently** → the seam in D1 is the mitigation; a
  deployment can never have the host source, so the branch is real and must stay legible.
- **An existing self-host Connector keeps its stored PAT** → this change adds no migration path off it.
  A follow-up, so the decision's benefit reaches Projects that already exist.
- **Compensating delete instead of one transaction** (D4) → mitigated by doing every fallible thing
  before any write; the residual window is an infrastructure failure between two writes.

## Migration Plan

One EF migration on the Backlog schema adding the Connector's credential-source column, nullable, so
every existing Connector reads back as the named-secret source with no behaviour change. No data
backfill. Rollback is the down migration; no Connector configured before this change is touched.

## Open Questions

- Whether the self-host form should **prefer** the host path or merely offer it. ADR-0028 explicitly
  left this open and this change does not decide it: the host path is what a Connector saved with no
  credential resolves to, and the form offers both.
