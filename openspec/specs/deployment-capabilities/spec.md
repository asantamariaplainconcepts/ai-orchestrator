# deployment-capabilities

## Purpose

What a deployment tells its own portal about itself: the capabilities that follow from how it was
composed at startup, so a client renders what can succeed here without inferring the habitat from
refusals.

## Requirements

### Requirement: a deployment tells its portal what it is

The product SHALL expose one read answering what this deployment can offer, so a client never has
to infer a habitat by provoking a refusal. The answer SHALL be derived from the same habitat
question the modules ask, so the portal and the API cannot disagree about which deployment this
is.

The answer SHALL name **capabilities**, not configuration: whether the code-source surface exists,
and whether a credential can be named in a secret store the deployment composes. It SHALL NOT
carry the mode string, a vault URI, or any other value a client could re-derive rules from — a
client that learns what it may offer stays correct when the underlying condition changes, and one
that learns the mode reimplements the rules and drifts.

Each capability SHALL be derived from the condition that actually makes it succeed, which is not
always the posture. "A secret can be stored" SHALL follow whether this deployment composed a store
that accepts writes — a self-host deployment configured with one stores perfectly well, so
deriving it from the posture would remove a working option from the habitat it was meant to
serve.

The read SHALL disclose nothing about projects, people or configuration values, and SHALL
therefore be answerable before anyone signs in — a sign-in screen has to know what kind of
deployment it is on.

#### Scenario: the portal asks instead of inferring

- **WHEN** the portal needs to know whether a surface exists
- **THEN** it reads the capabilities once, and no request is made whose purpose is to be refused

#### Scenario: capabilities, not configuration

- **WHEN** the capabilities are read
- **THEN** the answer states what may be offered, and carries no mode string, vault URI or other
  value from which a client could re-derive the rules

#### Scenario: storing a secret follows the store, not the posture

- **WHEN** a deployment composes no store that accepts writes
- **THEN** storing a secret is reported as unavailable, whatever the posture is

#### Scenario: a self-host deployment with a store can still store

- **WHEN** a self-host deployment composes a store that accepts writes
- **THEN** storing a secret is reported as available

#### Scenario: readable before sign-in

- **WHEN** the read is made by a caller who has not signed in
- **THEN** it answers, and discloses no project, person or configuration value

### Requirement: the Local locus is declared, and its absence carries the reason

The capabilities answer SHALL state whether a folder on the operator's machine is reachable from
the process that would work in it, and where it is not, SHALL carry the reason as a sentence —
the same pattern the store remedy follows.

The fact SHALL follow the habitat's own **declaration** (its composition sets the reason), never
an inference from the runtime environment (ADR-0010): a container the operator deliberately
mounted is reachable, and only the composition knows.

#### Scenario: compose self-host withholds the Local locus with its reason

- **WHEN** the capabilities are read on a deployment whose composition declares the Local locus
  unavailable
- **THEN** the answer says the local folder cannot be used here and carries the declared reason

#### Scenario: the dev loop keeps the locus

- **WHEN** the capabilities are read on a self-host deployment whose composition declares nothing
- **THEN** the local folder is offered exactly as before this change
