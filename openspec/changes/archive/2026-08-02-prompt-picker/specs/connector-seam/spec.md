# connector-seam — delta for prompt-picker

## ADDED Requirements

### Requirement: a repository directory's files can be listed

The seam SHALL expose the file names of a repository directory at the default branch, read live
from the vendor with the project credential resolved by name at the moment of the call (BR-010).
The read SHALL return names only — no content — one directory level deep, and no vendor noun SHALL
appear in the seam's types. It exists so the portal can offer the prompts that actually exist
(#215) without the browser ever holding a credential or talking to a vendor.

An absent directory SHALL be an ordinary outcome, distinguishable from a vendor refusal — the
caller renders one as "nothing there yet" and the other with the vendor's reason. Neither SHALL be
an exception that takes the caller down.

The GitHub implementation SHALL be exercised; the Azure DevOps implementation SHALL sit beside it
per ADR-0005 — translation unit-tested in both directions, labelled a stated hypothesis in the
class, unexercised against a real organisation.

#### Scenario: listing a directory that exists

- **WHEN** a directory containing files is listed through the seam
- **THEN** the file names are returned, without content, one level deep

#### Scenario: the directory is not there

- **WHEN** a path that does not exist on the default branch is listed
- **THEN** the result says the directory is absent, and no error escapes to the caller

#### Scenario: the vendor refuses

- **WHEN** the vendor rejects the read (revoked credential, unreachable repository)
- **THEN** the result carries the vendor's refusal for the caller to render, and nothing throws
