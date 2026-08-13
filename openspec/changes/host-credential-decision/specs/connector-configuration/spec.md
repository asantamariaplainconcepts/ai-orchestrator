## MODIFIED Requirements

### Requirement: a credential path is offered only where it can succeed

The Connector form SHALL offer a vendor credential in every deployment, because the backlog is
remote wherever the code lives: reading Stories, verifying the Connector and writing labels need
one in every posture. Only a Local Run's workspace skips a vendor credential, and that is git
rather than the backlog.

**Whether the host's own identity may stand in for that credential SHALL be recorded, not left
implicit.** OPN-006 asks whether a self-host deployment may authenticate vendor reads — and, per
#347, vendor writes — as the machine rather than as a credential the operator supplied. Exactly one
of two answers SHALL be stated here, in requirement text, and its ADR SHALL be cited:

- **the host's identity SHALL NOT authenticate vendor access**, in any posture, and the paragraph
  above stands unqualified; or
- **the host's identity MAY authenticate vendor access in self-host**, in which case this
  requirement SHALL name: which vendor operations it covers, how the credential is resolved
  non-interactively, what the operator is told to grant given that the resolution carries no scope,
  and what a Run's record says about which identity acted.

Leaving the question to inference SHALL NOT satisfy this requirement. *(The answer is written by the
change that closes OPN-006; this text is what it replaces.)*

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

#### Scenario: the host-identity question is answered in the text

- **WHEN** this requirement is read to learn whether a self-host deployment may reach the vendor as
  the machine rather than as a supplied credential
- **THEN** it states one of the two answers explicitly and cites the ADR that decided it

### Requirement: the product states the permissions it needs

The product SHALL state which permissions a credential needs for **this project's configuration**,
in the vendor's own vocabulary — the names a person selects while minting a token, not the
product's internal capability names. The statement SHALL appear where a credential is supplied,
and the same list SHALL be documented where somebody minting one will look.

The list SHALL be derived from the same capability set verification uses, so a capability cannot
exist without saying what to grant for it, and the documentation cannot drift from the code.

**Where a credential is not minted through this product, the guarantee SHALL be stated at the
strength it actually holds.** A credential resolved from the host carries no scope, no capability
and no naming of the application it was minted for, so the permission list for such a credential
SHALL NOT be presented as derived from what that credential holds. Whether such a path exists at all
is decided by the change that closes OPN-006; if it does, this requirement SHALL state what the
operator is told instead, and SHALL NOT report an unknown permission as a satisfied one.

#### Scenario: the form says what to grant

- **WHEN** an Admin supplies a credential
- **THEN** the permissions this configuration requires are stated in the vendor's own vocabulary

#### Scenario: a local code source asks for less

- **WHEN** the configuration's code source is a local folder
- **THEN** the stated permissions exclude cloning, pushing and opening pull requests

#### Scenario: an unminted credential is not described as verified-by-derivation

- **WHEN** a credential the operator did not mint through this product is used, where that is
  permitted at all
- **THEN** the permission statement does not claim to be derived from that credential's own scopes
