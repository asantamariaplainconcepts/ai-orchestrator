## MODIFIED Requirements

### Requirement: vendors are reached through one abstraction

All vendor access SHALL go through a single connector abstraction exposing verification and Story
retrieval. Vendor SDK types SHALL NOT appear outside the implementation that owns them, so a
second vendor can be added without touching any caller.

**What the seam carries as its credential SHALL be stated, not implied by a parameter type.** Every
method takes a credential today, and the absence of any other shape is legible only from the fact
that the parameter is a `string`. Exactly one of two answers SHALL be recorded here, citing the ADR
that closed OPN-006:

- **the credential is always a resolved secret value**, supplied by the operator and resolved by
  name at the moment of use (BR-010), and no other shape reaches the seam; or
- **the credential may instead name a resolution the host performs**, in which case this requirement
  SHALL state that a vendor implementation SHALL NOT be able to tell the two apart — the seam
  resolves before dispatch — and that any host resolution SHALL be non-interactive and SHALL fail
  with a stated reason rather than blocking, so no polling cycle can stall on a credential prompt.

A shape that works for one vendor and not the other SHALL NOT be introduced, whichever answer is
recorded: a second vendor slotting in without touching the polling loop, the mirror, or the API is
this requirement's existing promise, and an authentication mode available only to GitHub would break
it. *(The answer is written by the change that closes OPN-006; this text is what it replaces.)*

#### Scenario: adding a vendor

- **WHEN** a second vendor implementation is added
- **THEN** the polling loop, the mirror, and the API are unchanged

#### Scenario: no SDK leakage

- **WHEN** code outside the GitHub implementation is inspected
- **THEN** no GitHub SDK type appears in a signature, a domain type, or an API contract

#### Scenario: the credential's shape is stated

- **WHEN** the seam is read to learn whether a credential is always a resolved value
- **THEN** the requirement states one of the two answers explicitly and cites the ADR that decided it

#### Scenario: no vendor-specific authentication mode

- **WHEN** an authentication mode is available for one vendor
- **THEN** it is available for the other, or it is not introduced
