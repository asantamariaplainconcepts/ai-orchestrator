## Context

The Connector is the single seam through which vendor backlogs are reached
([`IBacklogConnector.cs`](../../../src/modules/Backlog/AiOrchestrator.Modules.Backlog/Connectors/IBacklogConnector.cs)).
Every one of its fourteen methods takes `string token`, and both implementations resolve a client
from it (`clientFactory.Create(coordinates.Owner, token)`). BR-010 keeps the *value* out of Postgres,
logs, API responses and telemetry: what is stored is a **name**, resolved at the moment of use.

Two places in this product already delegate to the host's own identity, and they bound the question:

1. **The agent's process.** `AgentCredentialEnvironment.For` exports no vendor token when the process
   host does not supply credentials, because *"an exported empty variable SHADOWS whatever auth the
   host's own tooling holds"*. A Local Run's agent already reaches the vendor as the machine's user.
2. **Git transport.** [`connector-configuration`](../../specs/connector-configuration/spec.md) states
   that a local-folder project's code capabilities are neither verified nor required, because *"its
   working copy is the host's own and git runs with the host's credentials"*.

Both delegate the host's tooling **doing its own job**: the agent authenticates itself, git
authenticates git. Neither has the *product* calling a vendor API under an ambient identity. That is
the line OPN-006 asks whether to cross, and the distinction the ADR has to engage with rather than
collapse.

Two requirements in `connector-configuration` are the load-bearing constraints, because a
host-derived credential cannot satisfy either as currently written:

- *"the product states the permissions it needs"* — **in the vendor's own vocabulary, the names a
  person selects while minting a token**, derived from the same capability set verification uses.
- *"the credential is verified before the Connector is stored"* — a write capability is verified **by
  asking the vendor what the credential may do, never by doing it**, with an explicit escape hatch:
  a capability the vendor cannot answer without acting is reported *not verifiable*, with its reason,
  and saving is allowed.

## Goals / Non-Goals

**Goals:**

- Close OPN-006 with a recorded decision: an ADR, a `DEC-069`, and the spec requirement text the
  decision produces.
- Evaluate all four options the issue names, each against the same criteria, with the blast radius on
  the Connector seam, on `VerifyAccess`, and on BR-010's by-name resolution stated for each.
- State which options #347 eliminates, and on what ground.
- State, for the winning option, what a Run's log says about which identity touched the vendor.
- Unblock #347 or refuse it explicitly — either is a legitimate outcome, and leaving it ambiguous is
  not.

**Non-Goals:**

- Implementing any option. This change ships no code (RULE-006).
- The posture gating itself (#222), and deriving Connector coordinates from a folder's `origin` —
  both belong to #347 and neither depends on this decision.
- Deciding anything about the **agent's** credentials. That is settled and is cited here only as
  precedent.

## Decisions

### D1 — the criteria every option is judged against

Five, applied identically to (a), (b), (c) and (d), so the comparison is not shaped per option:

1. **Vendor symmetry.** Does it work for both GitHub and Azure DevOps (DEC-045's promise that a second
   vendor slots in without touching the polling loop, the mirror, or the API)?
2. **Seam blast radius.** What must change in `IBacklogConnector`'s fourteen signatures and both
   implementations?
3. **Verification.** Can `VerifyAccess` still answer *"can this credential do our reads"* before the
   Connector is stored, and can the product still state **what to grant** in the vendor's own
   vocabulary?
4. **BR-010 and by-name resolution.** What exists at rest, and under what name is it resolved at the
   moment of use?
5. **Audit honesty.** What does a Run's record say about which identity touched the vendor?

### D2 — the decision is made against evidence, not framing

The issue's own amendment retires one argument (*"we have never trusted the host's identity"*), and
option (d) rests on a claim that must be **exercised, not asserted**: whether a credential helper's
output can authenticate vendor **API** calls, and whether the product can know that in advance.

The exercised finding is recorded in [`evidence.md`](evidence.md) §1 and is decisive for (d): the
credential-helper protocol's richest legal output is `username`, `password`, `oauth_refresh_token`
and `password_expiry_utc`. **It carries no scope, no capability, and no vendor identity.** A product
receiving a helper credential therefore learns a secret and a username, and nothing whatsoever about
what the secret is permitted to do.

That does not by itself refuse (d) — reads can be verified by performing them, since they are reads,
and the spec's *not verifiable* escape hatch already exists for writes. It refuses the *derived*
half of criterion 3: the product cannot tell the operator what to grant, because it does not know
what was granted, by whom, or to which application. Whichever way the ADR goes, it must say so.

### D3 — `CredentialSource` is named, not reinvented

`IAgentProcessHost.CredentialSource` exists so that *"the source is never left to inference"*. If the
decision permits delegation, the Connector's answer to criterion 5 is the same shape applied to a new
seam — not a new mechanism. If the decision refuses delegation, criterion 5 is answered trivially
(the named secret) and the ADR says that, rather than leaving it unstated.

### D4 — the decision, and where it is written

The ADR is the decision. It is written in the implementation stage, from the evaluation in tasks
group 2, and the spec deltas are filled in from its conclusion — deliberately **not** guessed in this
design, because guessing it here and confirming it there would make the evaluation ceremonial. What
is fixed now is the framework (D1), the evidence discipline (D2) and the vocabulary (D3).

**Alternatives considered for this design's own shape:** writing the conclusion into the design and
having the ADR restate it. Rejected — the ADR would then be a transcription rather than a record, and
[`decision-records`](../../specs/decision-records/spec.md) requires an ADR to cite the evidence that
produced it.

## Risks / Trade-offs

- **A permissive decision doubles the Connector's authentication modes, permanently.** A governed
  deployment has no host identity, so delegation can only ever be self-host — meaning the seam
  carries two shapes forever. → The ADR must state this as an accepted cost, not discover it in #347.
- **A helper credential can expire mid-Run** (`password_expiry_utc` is part of the protocol), where a
  stored PAT does not silently rotate. → Both directions are real: the host refreshes without the
  operator acting, and a Run can lose auth in flight. The ADR names which it is buying.
- **A credential helper may prompt.** `git credential fill` in a long-lived server process with no
  tty can block or fail, which would stall UC-009's polling loop. → Any permissive decision must
  mandate non-interactive resolution with a bounded failure, rather than leaving it to the
  implementation to discover.
- **This change closes a decision but ships nothing runnable**, so its correctness is only visible
  when #347 implements against it. → The spec deltas are the check: they make the decision testable
  text rather than prose in an ADR.
- **The decision is being made unattended** (`/aio:ship`, DEC-068 / ADR-0027), so nobody reads the
  reasoning before it lands. → An ADR is supersedable by construction, and the retro entry and PR body
  both mark the route, so overturning it is cheap and the fact that nobody reviewed it is countable.

## Open Questions

None blocking. The one factual unknown option (d) rested on — whether a helper's output carries
enough for the product to reason about vendor API access — was exercised and is recorded in
`evidence.md` §1.
