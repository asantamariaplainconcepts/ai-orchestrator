# Tasks — edit-connector-keeps-credential

- [x] 1.1 The Validator keeps "not both" and no longer refuses "neither" (design D1).
- [x] 2.1 The handler loads the Connector before choosing the credential, so the stored name is
      available before resolution (design D3).
- [x] 2.2 With no credential supplied: no Connector refuses as today; a Connector reuses its stored
      secret name (design D1, D2).
- [x] 2.3 The reuse path re-verifies through `VerifyAccess` and stores nothing (design D2).
- [x] 3.1 A vendor switch with no new credential is refused naming why (design D4).
- [x] 4.1 The reuse path carries the Admin role check (design D5).
- [x] 5.1 The Settings form makes the credential inputs optional on an existing Connector, drops the
      submit guard that required one, and says that the stored credential is kept (design D6).
- [x] 5.2 i18n keys for the optional-credential hint.
- [x] 6.1 Functional tests: reuse saves a setting and re-verifies; reuse on a project with no Connector
      still refuses; a failing probe on new coordinates refuses named; a vendor switch refuses; a
      non-Admin reuse refuses; both-supplied still refuses; rotation still replaces.
- [x] 6.2 A test asserting the response and the stored row carry no token value (BR-010).
- [x] 7.1 E2E: the prompts directory saves on an existing Connector with the Token field left empty —
      the case #150 exposed.
- [ ] 8.1 CI green; evidence on #160.
