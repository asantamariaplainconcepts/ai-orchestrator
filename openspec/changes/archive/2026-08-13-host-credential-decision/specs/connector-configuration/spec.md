## MODIFIED Requirements

### Requirement: a credential path is offered only where it can succeed

The Connector form SHALL offer a vendor credential in every deployment, because the backlog is
remote wherever the code lives: reading Stories, verifying the Connector and writing labels need
one in every posture. Only a Local Run's workspace skips a vendor credential, and that is git
rather than the backlog.

**A self-host deployment MAY instead authenticate as its host; a governed deployment SHALL NOT**
(OPN-006, closed by ADR-0028 / DEC-069). Where the host path is used:

- It SHALL cover **both reads and writes** — every vendor operation the Connector performs, not a
  subset, because a configuration that still requires a minted credential for writes has not spared
  the operator minting one.
- The credential SHALL be resolved from the **machine's git credential helper**, through the same
  resolver seam every other resolution uses, per read. A vendor-specific CLI SHALL NOT be the
  source: an authentication mode available for one vendor and not the other is forbidden by
  `connector-seam`.
- Resolution SHALL be **non-interactive**. A helper that cannot answer without prompting SHALL fail
  with a stated reason rather than wait, so a polling cycle can never stall on a credential prompt.
  It SHALL NOT fall back to an empty or default credential, as no resolution may.
- The product SHALL report **which identity touched the vendor** — the named secret, or the host's
  credential helper and the host it was asked about — so the source is never left to inference.

A governed deployment SHALL name a credential, as before. The difference between the two habitats is
deliberate: a deployment has no host identity to borrow, and its machine is not the operator's.

**Which of the two ways to supply it are offered SHALL follow what the deployment can do, not what
posture it is in.** Naming an existing secret SHALL always be available: a resolver is composed in
every habitat. Pasting a token SHALL be offered only where the deployment composes a secret store
that accepts writes — without one, every paste ends in the store's own refusal, and offering a
control whose only outcome is that refusal is the failure this rule removes.

Where pasting is unavailable, naming SHALL be the credential field rather than a secondary
control, and the form SHALL state the remedy the unavailable store already names, so an operator
learns how to gain the option rather than only that they lack it.

The condition SHALL come from the deployment's capabilities read — never from a client re-deriving
it from a posture, and never from provoking a refusal.

#### Scenario: a credential is always askable

- **WHEN** the Connector form renders in any deployment
- **THEN** at least one way to supply a vendor credential is offered

#### Scenario: pasting needs somewhere to put it

- **WHEN** the deployment composes no secret store that accepts writes
- **THEN** pasting is not offered, naming is the credential field, and the store's own remedy is
  stated

#### Scenario: both ways where a store exists

- **WHEN** the deployment composes a store that accepts writes
- **THEN** pasting leads and naming is available beside it, as before

#### Scenario: the posture does not decide this

- **WHEN** a self-host deployment composes a writable store
- **THEN** pasting is offered, exactly as it is in a cloud deployment

#### Scenario: a self-host Connector authenticates as its host

- **WHEN** a self-host deployment configures a Connector using the host path
- **THEN** vendor reads and writes both proceed on a credential resolved from the machine's git
  credential helper, and no vendor token is stored

#### Scenario: a deployment cannot borrow a host identity

- **WHEN** a governed deployment configures a Connector
- **THEN** the host path is not offered, and a credential is named as before

#### Scenario: a helper that would prompt fails instead of waiting

- **WHEN** resolution through the host's credential helper cannot complete without prompting
- **THEN** it fails with a stated reason, the polling cycle does not stall, and no empty or default
  credential is substituted

#### Scenario: the record says which identity acted

- **WHEN** a Run's record is read to learn how the vendor was reached
- **THEN** it names either the secret the Connector named, or the host's credential helper and the
  host it was asked about

### Requirement: the product states the permissions it needs

The product SHALL state which permissions a credential needs for **this project's configuration**,
in the vendor's own vocabulary — the names a person selects while minting a token, not the
product's internal capability names. The statement SHALL appear where a credential is supplied,
and the same list SHALL be documented where somebody minting one will look.

The list SHALL be derived from the same capability set verification uses, so a capability cannot
exist without saying what to grant for it, and the documentation cannot drift from the code.

**Where the credential is resolved from the host, the statement SHALL be documented rather than
derived, and SHALL say so.** The git credential-helper protocol carries no scope, no capability and
no naming of the application a credential was minted for, so the product cannot determine what a
host-resolved credential may do. It SHALL NOT present such a list as derived from that credential's
own permissions, and SHALL NOT report an unknown permission as a satisfied one. Where a vendor
discloses a credential's scopes on its own API responses, the product MAY use that to enrich the
statement; it SHALL NOT rely on it, because it is a vendor's courtesy and not a property of the
resolution.

#### Scenario: the form says what to grant

- **WHEN** an Admin supplies a credential
- **THEN** the permissions this configuration requires are stated in the vendor's own vocabulary

#### Scenario: a local code source asks for less

- **WHEN** the configuration's code source is a local folder
- **THEN** the stated permissions exclude cloning, pushing and opening pull requests

#### Scenario: a host-resolved credential is not described as verified-by-derivation

- **WHEN** the credential is resolved from the host's credential helper
- **THEN** the permission statement is presented as what this configuration requires, not as what
  that credential holds, and no unknown permission is reported as satisfied
