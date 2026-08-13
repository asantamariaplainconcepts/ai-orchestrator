## MODIFIED Requirements

### Requirement: vendors are reached through one abstraction

All vendor access SHALL go through a single connector abstraction exposing verification and Story
retrieval. Vendor SDK types SHALL NOT appear outside the implementation that owns them, so a
second vendor can be added without touching any caller.

**The seam SHALL receive a resolved credential value, and SHALL NOT be able to tell where it came
from** (OPN-006, closed by ADR-0028 / DEC-069). A credential may be resolved from a named secret or,
in self-host, from the machine's git credential helper; that choice belongs to the resolver, which
already sits upstream of this seam, and a vendor implementation SHALL NOT branch on it. This is what
keeps the decision to authenticate as the host out of fourteen method signatures and out of both
vendor implementations.

**No authentication mode SHALL exist for one vendor and not the other.** A mode available only where
a particular vendor CLI happens to be installed would break this requirement's standing promise that
a second vendor slots in without touching the polling loop, the mirror, or the API — which is why the
host path is the git credential helper, which both vendors have, rather than a vendor's own CLI.

#### Scenario: adding a vendor

- **WHEN** a second vendor implementation is added
- **THEN** the polling loop, the mirror, and the API are unchanged

#### Scenario: no SDK leakage

- **WHEN** code outside the GitHub implementation is inspected
- **THEN** no GitHub SDK type appears in a signature, a domain type, or an API contract

#### Scenario: the seam cannot tell how its credential was obtained

- **WHEN** a Connector's credential is resolved from the host's credential helper rather than from a
  named secret
- **THEN** the seam's signatures are unchanged and no vendor implementation behaves differently

#### Scenario: no vendor-specific authentication mode

- **WHEN** an authentication mode is available for one vendor
- **THEN** it is available for the other, or it is not introduced
