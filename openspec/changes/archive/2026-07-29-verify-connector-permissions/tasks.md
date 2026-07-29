# Tasks — verify-connector-permissions

- [x] 1.1 `Translate` distinguishes a permission refusal from an outage (design D3), carrying the
      vendor's own reason. Four causes, four errors. This lands first because D6 depends on it.
- [x] 2.1 `VerifyAccess` returns a verdict per capability (design D2) — Stories and documents, each
      with success and the vendor's reason on refusal — implemented for both vendors.
- [x] 3.1 The probe performs the reads the product performs (design D1) and writes nothing; a
      document path that does not exist is a pass, not a refusal (design D6).
- [x] 4.1 `ConfigureConnector` refuses naming the capability and the vendor's reason, and stores
      nothing when any capability is refused.
- [x] 5.1 One query slice for the on-demand test (design D4), calling the same probe (design D5),
      writing nothing.
- [x] 6.1 The Settings panel offers the test and renders the per-capability result, four states,
      both themes, copy through the i18n catalogue.
- [x] 7.1 Tests: a token that lists Stories but is refused contents fails the save naming the
      capability; a missing document path passes; a 403 reads as permission and not as unreachable;
      the four causes stay four distinct errors; the on-demand test reports per capability and
      leaves the Connector untouched; the save path and the test path call the same probe.
- [ ] 8.1 CI green; evidence on #132.
