# connector-configuration — delta for least-privilege-connector

## MODIFIED Requirements

### Requirement: the credential is verified before the Connector is stored

Saving a Connector SHALL perform live calls to the vendor using the supplied credential, and SHALL
store the Connector only if they succeed. What is verified SHALL be **every capability this
project's configuration will exercise** — not a fixed pair. Listing the repository's Stories and
reading a document are always among them; the writes the configuration will use are too. A
credential that can do one and not another SHALL NOT be accepted, because a permission missing at
save is a Run failing in front of somebody who did not configure it.

**The capability set SHALL follow the configuration.** A project whose code source is a local
folder SHALL NOT have the code capabilities verified or required: its working copy is the host's
own and git runs with the host's credentials, so nothing will clone, push or open a pull request
with this credential. An unrequested permission SHALL NOT be reported as a missing one.

Verification SHALL be read-only: no label, comment, branch, file or pull request is created or
modified by it, in any habitat. A write capability SHALL therefore be verified by asking the
vendor what the credential may do, never by doing it.

Where a vendor cannot answer that question without acting, the capability SHALL be reported
**not verifiable**, carrying the reason — and saving SHALL be allowed. An unanswerable question is
not a refusal, and reporting it as a pass would manufacture confidence nobody earned.

A failure SHALL be reported as RFC 7807 ProblemDetails naming **which capability** was refused and
carrying the vendor's own reason for it. The report SHALL distinguish four causes, because they
have four different fixes: an unreachable vendor, an unknown repository, a rejected credential, and
a credential the vendor refused for lack of permission. A vendor that answered SHALL NOT be
reported as unreachable.

Absence SHALL NOT be read as refusal: a document path that does not exist SHALL satisfy the
document capability, because "this path is empty" and "you may not look" are different answers and
only the second is a refusal.

#### Scenario: a credential that cannot read Stories is refused

- **WHEN** a Connector is saved with a credential the vendor refuses for the Stories read
- **THEN** it is not stored, and the refusal names that capability with the vendor's reason

#### Scenario: a credential that cannot write what the configuration needs is refused

- **WHEN** a Connector whose configuration will write labels is saved with a credential lacking
  that permission
- **THEN** it is not stored, and the refusal names that capability — rather than the Connector
  being stored and the permission being discovered inside a Run

#### Scenario: a local code source does not require code permissions

- **WHEN** a Connector with a local-folder code source is verified
- **THEN** the clone, push and pull-request capabilities are neither required nor reported missing

#### Scenario: an unanswerable capability is reported, never assumed

- **WHEN** the vendor cannot say whether a write is permitted without performing it
- **THEN** that capability is reported not verifiable with its reason, the Connector is stored,
  and nothing was written to verify it

#### Scenario: verification writes nothing

- **WHEN** any credential is verified, in any habitat
- **THEN** no label, comment, branch, file or pull request has been created or modified

## ADDED Requirements

### Requirement: the product states the permissions it needs

The product SHALL state which permissions a credential needs for **this project's configuration**,
in the vendor's own vocabulary — the names a person selects while minting a token, not the
product's internal capability names. The statement SHALL appear where a credential is supplied,
and the same list SHALL be documented where somebody minting one will look.

The list SHALL be derived from the same capability set verification uses, so a capability cannot
exist without saying what to grant for it, and the documentation cannot drift from the code.

#### Scenario: the form says what to grant

- **WHEN** an Admin supplies a credential
- **THEN** the permissions this configuration requires are stated in the vendor's own vocabulary

#### Scenario: a local code source asks for less

- **WHEN** the configuration's code source is a local folder
- **THEN** the stated permissions exclude cloning, pushing and opening pull requests
