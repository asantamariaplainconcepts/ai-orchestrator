# Proposal: local-owner-identity

## Why

Issue #119 (Foundation). Running this product on your own machine should cost an `aspire run` or
a `docker compose up` and nothing else — the operative half of DEC-049. Requiring an Entra tenant
to see a screen would void that, and OPN-002 has said since the charter that what is unverified
is exactly this: *"a workable local-dev + functional-test auth strategy exists (Entra cannot be
containerized)"*.

The answer is not a switch that turns authentication off. **The habitat decides who you are**: on
your own machine you are the owner and there is nobody else; on the web you are whoever signed
in. One seam, two answers, and the code above it never asks whether authentication exists.

## What changes

- **The identity seam** — the system's first principal (design D1). Nothing today has one:
  BR-009 is documented and unimplemented, and this is what gives the Entra trio (#11–#13)
  something to plug into rather than something to invent.
- **The local owner**: a fixed principal holding the Admin role, named so the portal and Run
  attribution show a person rather than a blank.
- **Two locks against reaching production** (design D2): Terraform never sets the value, and the
  server refuses to start when it is set alongside a Production environment or a non-loopback
  public URL — the shape of the guard that already refuses to start a worker without a database.
- **The unauthenticated hosted state announces itself** (design D3). Azure runs with no
  authentication today and nothing says so; until the Entra slice lands, starting that way logs a
  warning naming OPN-002.
- **Tests run as the local owner**, which is what closes OPN-002's half (b): they need a real
  principal and can now have one without a tenant.

## Impact

- Specs: `backend-architecture` (one ADDED requirement).
- Code: the seam in BuildingBlocks, its local implementation and the startup guard in the host;
  the portal shows who it thinks you are.
- No schema change, no endpoint change. Authorization checks themselves stay where they are —
  none exist yet, and inventing them here would be a second slice (RULE-002).

## Out of scope

Entra ID and any identity provider (#11–#13); multiple local users; switching identity at
runtime; and OPN-002's half (a), which only the owner can verify.
