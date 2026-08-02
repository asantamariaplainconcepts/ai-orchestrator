## Why

Issue #222. The posture is a startup fact — `Identity:Mode=LocalOwner`, read through
`IdentityHabitat.IsSelfHost` — and two backend sites already ask it. The portal cannot: since #211
it infers the posture by sending a deliberately invalid `validate-path` and reading the 404. That
works, reads like a trick, and #211's own retro recorded it as owed.

An answer settles a second thing the form gets wrong. Naming an existing secret presupposes a
vault somebody else manages, and a self-host deployment composes none — `AddSecretResolution`
falls back when `Secrets:KeyVaultUri` is absent. Offering a local owner the *name* of a secret in
a vault they do not have is an option that cannot succeed.

What does **not** change: the token itself is offered in both postures. The backlog is remote
wherever the code lives, so reading Stories, verifying the Connector and writing labels need a
vendor credential either way — only a Local Run's *workspace* skips one, and that is git, not the
backlog. Whether that could ever change is its own decision (#223, OPN-006).

## What Changes

- A **capabilities read** answers what this deployment is and which surfaces exist, in one call,
  derived from the same `IdentityHabitat` question the modules ask — so the portal and the API
  cannot disagree about the habitat.
- The portal **asks** it instead of provoking a 404. The probe added by #211 is removed, not
  bypassed.
- **Self-host hides what cannot succeed:** naming an existing secret is not offered, because there
  is no vault to name one in. The token input stays.
- **Cloud is unchanged:** both credential paths, and no code-source UI anywhere (#211's rule, now
  read from the answer rather than inferred).

## Capabilities

### New Capabilities

- `deployment-capabilities`: what a deployment tells its own portal about itself — the posture and
  the surfaces that follow from it, asked once rather than inferred from refusals.

### Modified Capabilities

- `connector-configuration`: the credential's two paths become posture-dependent — naming an
  existing secret is offered only where a vault exists.

## Impact

- **Backend**: one query use case in the Projects module (beside `GetCurrentPrincipal`, which is
  the other read about this habitat), returning the posture and the derived surface flags. No
  change to `ConfigureConnector`, its validator, or `ValidateLocalPath`'s own 404 behaviour — the
  posture gate stays where it is; the portal simply stops using it as an oracle.
- **Frontend**: `useCodeSourceSurface` replaced by the capabilities query; the Connector panel's
  credential control reads it.
- **Unchanged**: BR-009, BR-010 (only which *ways* to supply a credential are offered — never the
  value), #220's essentials-first shape, #211's cloud rule.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
