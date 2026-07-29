# connector-configuration

## MODIFIED Requirements

### Requirement: the credential is verified before the Connector is stored

Saving a Connector SHALL perform live calls to the vendor using the supplied credential, and SHALL
store the Connector only if they succeed. What is verified SHALL be the reads the product itself
performs: listing the repository's Stories, and reading a document from the repository. A
credential that can do one and not the other SHALL NOT be accepted, because every conversational
action depends on the second and matching depends on the first.

Verification SHALL be read-only: no label, comment, branch, file or pull request is created or
modified by it, in any habitat.

A failure SHALL be reported as RFC 7807 ProblemDetails naming **which capability** was refused and
carrying the vendor's own reason for it. The report SHALL distinguish four causes, because they
have four different fixes: an unreachable vendor, an unknown repository, a rejected credential, and
a credential the vendor refused for lack of permission. A vendor that answered SHALL NOT be
reported as unreachable.

Absence SHALL NOT be read as refusal: a document path that does not exist SHALL satisfy the
document capability, because "this path is empty" and "you may not look" are different answers and
only the second is a refusal.

#### Scenario: credential cannot read the repository

- **WHEN** an Admin saves a Connector whose token cannot read the named repository
- **THEN** the save fails, the problem names the credential as the cause, and no Connector is
  stored

#### Scenario: repository does not exist

- **WHEN** an Admin saves a Connector naming a repository that does not exist
- **THEN** the save fails, the problem names the coordinates as the cause, and no Connector is
  stored

#### Scenario: a stored Connector is a working Connector

- **WHEN** any Connector exists in the system
- **THEN** its credential was verified against the vendor at the moment it was stored

#### Scenario: a credential that reads Stories but not documents

- **WHEN** an Admin saves a Connector whose token can list Stories and is refused the repository's
  contents
- **THEN** the save fails naming the document capability and carrying the vendor's reason, and no
  Connector is stored

#### Scenario: a refusal is not an outage

- **WHEN** the vendor answers that the credential lacks a permission
- **THEN** the failure is reported as a permission problem, distinct from an unreachable vendor,
  and it repeats what the vendor said

#### Scenario: an empty document path is not a refusal

- **WHEN** the configured document path does not exist in a repository the credential can read
- **THEN** verification succeeds and the Connector is stored

## ADDED Requirements

### Requirement: an Admin can test a stored Connector's credential on demand

An Admin SHALL be able to test a configured Connector's stored credential at any time, without
supplying a token and without reconfiguring anything. The test SHALL report per capability which
reads succeeded and which were refused, with the vendor's reason for each refusal.

The test SHALL use the same probe that gates saving, so the two cannot disagree about what a
working credential is. It SHALL change nothing: no write to the vendor, and a failing test SHALL
leave the stored Connector exactly as it was.

#### Scenario: a credential that still works

- **WHEN** an Admin tests a Connector whose credential can perform both reads
- **THEN** every capability is reported as succeeding

#### Scenario: a credential that has lost a permission

- **WHEN** a permission is revoked at the vendor after the Connector was stored, and an Admin tests
  it
- **THEN** the refused capability is named with the vendor's reason, and the others still report as
  succeeding

#### Scenario: testing changes nothing

- **WHEN** a test fails for any reason
- **THEN** the Connector is unchanged and nothing was written at the vendor
