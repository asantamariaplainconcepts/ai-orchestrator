# Tasks — store-secret-value

- [ ] 1.1 `ISecretStore` with `Store` and no read (design D1), composed by the host beside
      `ISecretResolver`: Key Vault where a vault is configured, the encrypted local table
      otherwise, and a refusing implementation that names what to do instead.
- [ ] 2.1 The local store: ASP.NET Core Data Protection, key ring persisted outside the database
      (design D4), its own table in the module that owns it. No bespoke cryptography anywhere.
- [ ] 3.1 `ConfigureConnector` accepts a token as an alternative to a secret name, derives the
      name from the project (design D2), and orders the work store → verify with the stored value
      → persist (design D3). Neither field present, or both, is a validation failure naming which.
- [ ] 4.1 Storing requires an Admin through `ICurrentPrincipal` (design D6) — #124's expired risk,
      now its safeguard.
- [ ] 5.1 The Connector records when its secret was last set; the API and the portal show the name
      and that time, never a value.
- [ ] 6.1 The portal's Connector form offers pasting the token or naming a secret, with the four
      states and both themes, copy through the i18n catalogue.
- [ ] 7.1 BR-010 reworded to its intent in `05-business-rules.md`; DEC-052 added to
      `10-locked-mvp-decisions.md` with the reasoning (design D5).
- [ ] 8.1 Tests: the paste path configures and stores; the response and the log carry no part of
      the value; rotation replaces and the next Run uses the new value; the naming path is
      unchanged; a store that cannot write refuses legibly and leaves the naming path working; a
      non-Admin is refused; a failed verification leaves no Connector; the local row is ciphertext.
- [ ] 9.1 CI green; evidence on #124.
