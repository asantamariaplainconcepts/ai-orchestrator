## Why

Issue #226. DEC-030 gave each project one PAT "covering backlog read/write and code
clone/push/PR", noting that finer scoping was post-MVP. Two things since then turned that breadth
from simple into costly:

- **A LocalFolder project never uses the code half** (#210/#211): the working copy is the host's
  own folder and git runs with the host's credentials, so the product clones nothing, pushes
  nothing and opens no pull request with that PAT. Every code scope on it is granted and unused.
- **The agent holds the PAT and runs unbounded** (#162). What that token *can* do is the
  containment that remains; a scope nobody needs is a capability handed to a prompt this product
  did not write.

The product also never says what it needs. No scope list is documented anywhere, and
`VerifyAccess` probes exactly two capabilities — listing Stories and reading a document. A PAT
that cannot write a label, transition a work item or open a pull request is therefore **stored as
verified** and fails later inside a Run, in front of somebody who did not configure it. That is
precisely the failure #132's per-capability verdict exists to prevent, applied to only half the
surface the product uses.

## What Changes

- **The required scopes follow the configuration.** The product names what *this* project needs,
  in the vendor's own vocabulary — and a LocalFolder project's list excludes the code scopes,
  because nothing will exercise them.
- **Verification covers the writes.** Every capability the configuration will use is probed, each
  reported per capability as the reads already are.
- **A write is verified without being exercised.** No label applied, no branch created, no work
  item touched — verification stays read-only in every habitat, as the spec already requires.
  Where a vendor cannot answer without acting, the capability is reported **not verifiable** with
  that reason, never as a pass.
- **A narrower token is refused at save**, naming the capability it lacks, instead of being stored
  and surprising a Run.
- **The list is documented** where somebody minting a token looks (`SELF-HOSTING.md`).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `connector-configuration`: verification widens from the two reads to every capability the
  configuration will exercise, gains a third verdict value for what a vendor cannot answer without
  acting, and the required scopes become configuration-dependent and stated.

## Impact

- **Backend**: the Connector seam's `VerifyAccess` grows the capabilities it probes and the
  configuration it is told about; both vendor implementations follow — GitHub exercised, Azure
  DevOps as ADR-0005's stated hypothesis. `ConfigureConnector` passes the configuration and renders
  the wider verdict; `CredentialVerdict` gains the not-verifiable outcome.
- **Frontend**: the Connector form states the required scopes for the current configuration, and
  the credential-test panel (#132) renders the third outcome.
- **Docs**: `SELF-HOSTING.md` carries the same list; a DEC records that breadth follows
  configuration, revising DEC-030's single all-covering PAT.
- **Unchanged**: BR-009, BR-010, the read-only rule for verification, and everything about how the
  credential is stored or resolved.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
