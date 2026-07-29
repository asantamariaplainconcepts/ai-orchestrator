# Tasks — store-secret-value

- [x] 1.1 `ISecretStore` with `Store` and no read (design D1), composed by the host beside
      `ISecretResolver`: Key Vault where a vault is configured, the protected local store
      otherwise, and a refusing implementation that names what to do instead.
- [x] 2.1 The local store: ASP.NET Core Data Protection over files in a configured directory, key
      ring persisted in a second one (design D4, revised from a table while implementing — see
      the design note). The host refuses to start if only one of the two paths is set. No bespoke
      cryptography anywhere.
- [x] 3.1 `ConfigureConnector` accepts a token as an alternative to a secret name, derives the
      name from the project (design D2), and orders the work store → verify with the stored value
      → persist (design D3). Neither field present, or both, is a validation failure naming which.
- [x] 4.1 Storing requires an Admin through `ICurrentPrincipal` (design D6) — #124's expired risk,
      now its safeguard.
- [x] 5.1 The Connector records when its secret was last set; the API and the portal show the name
      and that time, never a value.
- [x] 6.1 The portal's Connector form offers pasting the token or naming a secret, with the four
      states and both themes, copy through the i18n catalogue.
- [x] 7.1 BR-010 reworded to its intent in `05-business-rules.md`; DEC-052 added to
      `10-locked-mvp-decisions.md` with the reasoning (design D5).
- [x] 8.1 Tests: the paste path configures and stores; the response and the log carry no part of
      the value; rotation replaces and the next Run uses the new value; the naming path is
      unchanged; a store that cannot write refuses legibly and leaves the naming path working; a
      non-Admin is refused; a failed verification leaves no Connector; what the local store writes
      to disk is not the token, and a value written under another key ring says so rather than
      reading as missing.
- [ ] 9.1 CI green; evidence on #124.
