## 1. The capabilities read

- [x] 1.1 Add the query use case in the Projects module beside `GetCurrentPrincipal` (design D1),
      answering capability flags only — no mode string, no vault URI (design D2)
- [x] 1.2 Derive "the code-source surface exists" from `IdentityHabitat.IsSelfHost`, and "a secret
      can be named" from whether the deployment composed an `ISecretStore` (design D3)
- [x] 1.3 Readable without signing in, disclosing nothing about projects or people (design D4)
- [x] 1.4 Functional tests: both postures, and the store-present/absent split asserted separately
      from the posture so their coincidence today cannot hide a wrong derivation

## 2. The portal asks

- [x] 2.1 Replace `useCodeSourceSurface` with the capabilities query — **delete** the 404 probe
      rather than layering on it
- [x] 2.2 The Connector panel renders the code-source section from the capability
- [x] 2.3 The credential control offers "name an existing secret" only when the capability says a
      store exists; the token input is unconditional

## 3. Copy

- [x] 3.1 No new user-facing string beyond what the absence removes; verify the catalogue has no
      orphaned key left by the removed control

## 4. Proof

- [x] 4.1 Browser-preview verification in mock mode for both shapes: with a store (both credential
      paths, code source per posture) and without (token only)
- [x] 4.2 Full gates — build, tests, lint, spec validation, design-system validator — plus a grep
      of the e2e tier for the removed control's label, per the #220 retro's rule
