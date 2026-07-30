# Proposal: entra-sign-in

## Why

Issue #12 (ACT-001/ACT-002; UC-001; BR-009 groundwork; BR-010). Every later capability assumes an
identity exists, and today the hosted deployment authenticates nobody: `UnauthenticatedCaller` hands
the Admin role to anyone who can reach the address, warns about itself at startup, and its own doc
comment has said from the day it was written that *"when a provider lands this implementation is the
one that disappears"*. DEC-058 closed OPN-002, so the provider can land.

**The two execution modes are load-bearing and both survive** (design D2). `Identity:Mode=LocalOwner`
— a machine one person owns, implicit Admin, refuses to start beside provisioned infrastructure — is
untouched: `aspire run` from a clean checkout keeps working with no Entra, no configuration and no
sign-in. What changes is only the hosted mode, where Entra replaces the stopgap.

## What changes

- **The Server becomes a confidential OIDC client** (design D1), per DEC-058's BFF shape:
  Microsoft.Identity.Web, authorization code flow, the session an `HttpOnly` cookie — no token ever
  reaches the browser. The client secret arrives by vault reference (BR-010): configuration carries
  the secret's *name*.
- **Composition keys on configuration presence, not environment names** (design D2): Entra is wired
  when `AzureAd` configuration exists, exactly as the secret store keys on the vault URI — the lesson
  IdentityComposition already records is that environment names lie.
- **`ICurrentPrincipal` gains its hosted implementation** (design D3): the principal comes from the
  session's claims. Every signed-in user holds Admin *for now*, stated loudly: per-project roles are
  #13's slice, and inventing a mapping here would be proposing #13 by accident.
- **Unauthenticated behaviour splits by surface** (design D4): a browser navigation is challenged to
  Entra; an API call gets a `401` with a problem body, never a redirect to HTML.
- **Signed out is a real state** (design D5): a sign-out endpoint ends the cookie session and the
  Entra session (front-channel logout is already registered), landing on the signed-out page.
- **The self-host compose keeps its stopgap** (design D2): no `AzureAd` configuration means the
  warned `UnauthenticatedCaller`, unchanged — DEC-049's habitat has no tenant to sign into.
- **The test tiers change nothing** (design D6): functional tests keep injecting `ICurrentPrincipal`;
  E2E runs with no `AzureAd` configuration and behaves as today. That was DEC-058's second half.

## Impact

- Specs: `backend-architecture` — one MODIFIED requirement (the per-habitat identity, gaining the
  provider mode and keeping all four scenarios). `authentication` — new capability, one ADDED
  requirement (the sign-in flow itself).
- Code: `IdentityComposition` (the hosted branch), a principal adapter over `HttpContext.User`,
  Microsoft.Identity.Web in the Server, the sign-out endpoint, and the SPA reacting to `401` by
  offering sign-in. `/api/me` already exists and starts telling the truth.
- Config: `AzureAd__TenantId`, `AzureAd__ClientId`, and the vault reference for
  `entra-client-secret` — the values `entra-app.sh` printed and stored.

## Out of scope

- Per-project role assignment — #13, which lands on the same principal seam.
- Protecting individual endpoints differently; this slice authenticates the surface, BR-009's
  authorization matrix comes with #13.
- Any change to the LocalOwner mode, the self-host compose, or the test tiers.
- Webhook ingestion auth (BR-015 signs those; they are not user surfaces).
