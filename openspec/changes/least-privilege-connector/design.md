## Context

`VerifyAccess` today takes coordinates, a document path and a token, and answers a verdict over
two capabilities. Both are reads, and the spec forbids verification from writing anything — "no
label, comment, branch, file or pull request is created or modified by it, in any habitat" — for
the obvious reason: pressing save must not leave debris in somebody's repository.

That rule is why the writes were never verified, and it is the constraint this change has to work
inside rather than around. GitHub answers what a token may do without exercising it: the REST API
returns the authenticated app's permissions for a repository, and Octokit surfaces the repository
object's `Permissions`. Azure DevOps has no equivalent this project can claim — everything about
that vendor is a stated hypothesis (ADR-0005).

## Goals / Non-Goals

**Goals:**
- Ask for no permission the configuration will not use.
- Refuse at save what would otherwise fail inside a Run.
- Never manufacture confidence: an unanswerable question is reported, not assumed.

**Non-Goals:**
- Two credentials, or a GitHub App (DEC-030 already names the latter as the later shape).
- Removing the credential in self-host (#223).
- Verifying anything by doing it — the read-only rule stands unchanged.

## Decisions

**D1 — the capability set is a function of the Connector's configuration, computed in one place.**
Given a vendor and a code source, one function returns the capabilities this project will
exercise. `ConfigureConnector` uses it to decide what to verify; the form uses the same list to
say what to grant. Two derivations would eventually disagree about whether a LocalFolder project
needs push. *Alternative rejected:* probing everything always and merely *displaying* less — it
would refuse a correctly-scoped narrow token, which is the opposite of the goal.

**D2 — a third verdict value: not verifiable.** Passed and Refused cannot express "this vendor
will not tell me without me doing it". Collapsing that into Passed manufactures confidence;
collapsing it into Refused blocks a correct credential. The verdict carries the reason, the form
renders it as its own state, and saving is **allowed** — an unanswerable question is not a
refusal. *Alternative rejected:* exercising the write and undoing it — creating and deleting a
label in somebody's repository is exactly the debris the read-only rule forbids, and an undo that
fails leaves it behind.

**D3 — GitHub reads its own permission grant; Azure DevOps declares the gap.** The GitHub
implementation asks the vendor what the token may do and maps that onto the capabilities, so the
answer costs no write. The Azure DevOps implementation reports the write capabilities as **not
verifiable**, naming that no permission-introspection call is claimed for it — which is honest,
consistent with ADR-0005, and better than a pass nobody exercised.

**D4 — the scope list is content, in the vendor's vocabulary.** "Contents: read", "Issues: write"
— what a person selects while minting a token, not the product's internal capability names. It
lives beside the capability set so a new capability cannot be added without saying what to grant
for it.

## Risks / Trade-offs

- [Not-verifiable reads as a shrug] → it names the vendor and the reason, and the save that
  follows is an informed one. The alternative is a pass that means nothing, which is what exists
  today for every write.
- [A permissions read is itself a permission] → GitHub returns repository permissions on the
  ordinary repository read the probe already performs; no new grant is required to ask.
- [The scope list drifts from what the code does] → it is derived from the same capability set the
  probe uses (D1), so a capability with no scope entry is a compile-time gap, not a doc rot.
- [A LocalFolder project later switches to Repository with a narrow token] → saving that switch
  re-verifies against the new configuration and refuses, naming the missing capability. Narrowing
  by configuration is safe precisely because the configuration is what triggers verification.

## Migration Plan

Additive: existing Connectors keep their stored credential untouched, and nothing is re-verified
until somebody saves. A token broader than needed keeps working — this asks for less, it does not
reject more.

## Open Questions

(none)
