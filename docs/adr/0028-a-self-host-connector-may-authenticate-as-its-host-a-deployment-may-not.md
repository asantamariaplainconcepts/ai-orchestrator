# ADR-0028: A self-host Connector may authenticate as its host; a deployment may not

- **Status:** Accepted
- **Date:** 2026-08-13
- **Deciders:** repository maintainer (solo, DEC-003) — **but see "How this decision was made"**
- **Tags:** backend, security, self-host, connector

## Context

[#222](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/222) established that a
self-host deployment cannot hide the Connector's token: the backlog is remote wherever the code
lives, so reading Stories, verifying the Connector and writing labels each need a credential. The
`connector-configuration` spec says exactly that today.

**OPN-006 asks whether that is *necessarily* so.** Two places in this product already delegate to the
host's own identity:

1. **The agent's process.** `AgentCredentialEnvironment.For` exports no vendor token when the process
   host does not supply credentials, and states the reason as a rule: *"an exported empty variable
   SHADOWS whatever auth the host's own tooling holds, which is the opposite of what an unset
   credential means. #279 established the rule for the AI key…; #244 AC6 extended it to the vendor
   token, which a Local Run never resolves."* In local mode an agent already reaches the vendor as
   whatever the machine is logged in as, **by design**, with a rule written to protect that from being
   shadowed.
2. **Git transport.** `connector-configuration` excuses a local-folder project from the *code*
   capabilities entirely, because *"its working copy is the host's own and git runs with the host's
   credentials, so nothing will clone, push or open a pull request with this credential."*

So *"we have never trusted the host's identity"* is not an argument available to this decision. What
those two share, and what makes them narrower than what OPN-006 proposes, is that each delegates the
host's tooling **doing its own job**: the agent authenticates itself; git authenticates git. Neither
has the *product* calling a vendor API under an ambient identity. That is the line this ADR decides
whether to cross.

[#347](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/347) is blocked on this and
on nothing else, and needs vendor **writes** as well as reads.

### What was measured, not assumed

The option that turns on a factual question — delegating to the machine's **git credential helper** —
had that question exercised (`evidence.md` §1). A stand-in helper emitting every key the protocol
permits, read through `git credential fill` (git 2.48.1), returns at most:

```
protocol, host, username, password, oauth_refresh_token, password_expiry_utc
```

**There is no scope field, no capability field, and no field naming the application the credential was
minted for.** This is a property of the protocol, not of one machine's configuration. A product
resolving a credential this way learns a secret and a username, and nothing about what the secret may
do. It also learns, usefully, that the credential may **expire** and may carry a **refresh token**.

### The blast radius is smaller than the question implies

All fourteen `IBacklogConnector` methods take `string token`, which makes delegation look like a
fourteen-signature change. It is not. `connector-configuration` already requires that *"token values
SHALL be obtained through a single resolver abstraction"*, that *"the storage mechanism can change
without touching call sites"*, and that secrets *"resolve per read, not at startup"* — with a rotated
secret picked up on the next resolution, and a name that cannot be found failing loudly rather than
*"falling back to an empty or default credential"*.

Resolution therefore already sits **upstream of the seam**. A host-derived credential is another
resolver behind the seam that exists, not a second seam beside it.

## Decision

**We will permit a self-host deployment to authenticate its Connector as the host, resolved through
the machine's git credential helper, for both reads and writes — and we will refuse it in a governed
deployment.**

Concretely:

- **The credential source is the git credential helper**, not a vendor CLI. Both vendors have one, so
  no vendor is second-class — the promise in `connector-seam` that a second vendor slots in *"without
  touching the polling loop, the mirror, or the API"* is preserved.
- **It resolves behind the existing resolver seam**, per read, exactly as a named secret does. The
  Connector still stores a *name*; that name denotes the host's helper for a host rather than an entry
  in a secret store. BR-010 is not merely satisfied but strengthened: **nothing is at rest at all.**
- **Resolution is non-interactive and bounded.** A helper that cannot answer without prompting is a
  failure with a stated reason, never a wait — the polling loop must not be able to stall on a
  credential prompt. The existing rule that resolution *"never falls back to an empty or default
  credential"* already forbids the other half of this failure.
- **Refused in a governed deployment**, which has no host identity to borrow and where the machine is
  not the operator's. This follows
  [ADR-0021](0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md), which decided
  the same habitat asymmetry for an attached session on the same ground.

### The habitat answer, stated as its own paragraph

**Two rules, not one.** A self-host deployment may authenticate as its host; a governed deployment
may not, and must name a credential. This is a deliberate difference in product behaviour between
habitats, and it is the second time this repository has drawn that line in the same place and for the
same reason — a machine its operator owns is not one somebody else pays for or administers. One rule
was considered and rejected: applied permissively it would hand a shared deployment an ambient
identity nobody consented to; applied restrictively it would refuse self-host the thing that motivated
the question.

### What a Run's record says about which identity acted

The Connector reports a **credential source**, borrowing the shape
`IAgentProcessHost.CredentialSource` already uses on the agent's side — *"so the source is never left
to inference"*. It names either the secret the Connector named, or the host's credential helper and
the host it was asked about. It is **named rather than reinvented**: the same question already had an
answer in this codebase, and a second mechanism for it would be the drift this repository keeps
paying for.

### What the operator is told to grant

Honestly, and at the strength the guarantee actually holds. Today the product must state the
permissions a credential needs *"in the vendor's own vocabulary — the names a person selects while
minting a token"*, **derived** from the capability set verification uses. For a host-resolved
credential that derivation is impossible: the protocol carries no scope. The statement therefore
becomes **documented rather than derived**, and must say so. A vendor that discloses scopes on its own
API responses may be used to enrich it where it does, but that is a vendor's courtesy, not a
guarantee the product may promise.

Reads stay verifiable by performing them — they are reads, and `VerifyAccess` is read-only always.
Writes fall to the existing escape hatch: a capability the vendor cannot answer without acting is
reported **not verifiable**, carrying its reason, and saving is allowed.

## Consequences

- **Positive:** a self-host owner configures a Connector without minting a PAT, which is what #347
  asked for; nothing is stored at rest, so BR-010's exposure surface shrinks to zero on that path;
  credential rotation stops being the operator's chore, because the helper refreshes and resolution is
  already per-read; and the decision reuses two seams and one vocabulary that already exist rather than
  adding any.
- **Negative:** the product acquires **two credential sources** behind one resolver, permanently,
  because a deployment can never have this one. The permission statement degrades from *derived* to
  *documented* on the host path — a real weakening of a guarantee the spec makes today, and the reason
  the spec text must state it rather than let the form imply derivation. A write capability that
  cannot be verified means a self-host operator can save a Connector that fails later inside a Run,
  which is precisely the failure the verify-before-store requirement exists to prevent. A helper
  credential can **expire mid-Run**, where a named PAT does not silently rotate.
- **Neutral:** #347 is unblocked for both reads and writes, and inherits three obligations named here:
  non-interactive resolution, the credential-source report, and the honest permission statement.
  Whether the self-host form should *prefer* the host path or merely offer it is a UI question this ADR
  does not decide.

### The check that would catch a regression

The requirement text in `connector-configuration` and `connector-seam` is written so each obligation
is a scenario: that an unminted credential is never described as verified-by-derivation, that no
authentication mode exists for one vendor and not the other, and that the host-identity question is
answered in the text rather than by inference. A change that quietly re-derives permissions from a
host credential, or adds a GitHub-only path, fails a scenario rather than a reviewer's memory.

## Alternatives considered

- **(a) Status quo — a named credential in both postures** — rejected because it answers #347 with
  "no" while the two delegations already in this codebase show the trust model is not the obstacle. Its
  merits are real and were weighed: zero blast radius, and every verification guarantee intact.
- **(b) Delegate vendor reads to the host's `gh` CLI** — rejected on **vendor symmetry**. Azure DevOps
  has no equivalent, so this makes one vendor second-class and breaks `connector-seam`'s standing
  promise that a second vendor slots in without touching the polling loop, the mirror, or the API. The
  asymmetry is an artefact of framing the question around `gh`; option (d) is the same idea without it.
- **(c) A hybrid — reads delegate, writes name a credential** — rejected **twice over**. #347 covers
  writes, so it is eliminated by construction; and independently, it fails on its own terms, because an
  operator who must still mint a PAT for writes has not been spared minting a PAT. It buys two
  resolution paths and delivers none of the goal.
- **One rule for both habitats** — rejected; see the habitat paragraph above.
- **Delegating at the seam rather than at the resolver** — rejected because it would change fourteen
  signatures and let a vendor implementation see how its credential was obtained, which is exactly what
  the single-resolver requirement exists to prevent.

## How this decision was made

**This ADR was written, and merged to `main`, by `/aio:ship 223` in a single unattended run
(DEC-068 / [ADR-0027](0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)). No
human read it before it landed.** The issue it closes names ACT-001 — the owner — as the deciding
actor, so this is a decision made on the owner's behalf rather than by them, on evidence the issue
itself specified. It is recorded here because a reader weighing this decision should know how much
review it had, and because an ADR is supersedable by construction: overturning it costs one later
change, and ADR-0008 → ADR-0021 is the precedent for doing exactly that.

## References

- Closes **OPN-006**; recorded as **DEC-069** in
  [`10-locked-mvp-decisions.md`](../product/mvp/10-locked-mvp-decisions.md).
- Issue: [#223](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/223). Unblocks
  [#347](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/347). Informed by
  [#222](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/222).
- Evidence: `openspec/changes/archive/*-host-credential-decision/evidence.md` §1 (the credential-helper
  protocol probe), §3 (the seam's actual shape).
- Related: [ADR-0021](0021-a-developers-own-machine-may-hold-a-session-a-deployment-may-not.md) — the
  same habitat asymmetry, decided on the same ground.
- Code named rather than reinvented:
  [`AgentCredentialEnvironment.cs`](../../src/shared/AiOrchestrator.Infrastructure/Agents/AgentCredentialEnvironment.cs),
  [`IAgentProcessHost.cs`](../../src/shared/AiOrchestrator.Infrastructure/Agents/IAgentProcessHost.cs),
  [`IBacklogConnector.cs`](../../src/modules/Backlog/AiOrchestrator.Modules.Backlog/Connectors/IBacklogConnector.cs).
- Business rules: BR-010 (the rule under examination), BR-008.
