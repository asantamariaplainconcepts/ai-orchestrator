## 1. The capability set

- [ ] 1.1 One function from (vendor, code source) to the capabilities this project exercises
      (design D1) — used by verification and by the surface that states them, so they cannot
      disagree
- [ ] 1.2 Each capability carries its vendor-vocabulary scope name (design D4); a capability with
      no scope entry fails to compile rather than rotting the docs

## 2. Verification widens

- [ ] 2.1 `VerifyAccess` takes the configuration and probes every capability it names
- [ ] 2.2 `CredentialVerdict` gains the not-verifiable outcome with its reason (design D2); saving
      is allowed on it, refused only on an actual refusal
- [ ] 2.3 GitHub: read the repository's permission grant and map it onto the write capabilities —
      no write performed (design D3)
- [ ] 2.4 Azure DevOps: report the write capabilities as not verifiable, naming that no
      permission-introspection call is claimed (ADR-0005)
- [ ] 2.5 Functional tests: a write-lacking credential refused at save; a local-folder
      configuration neither requiring nor reporting code capabilities; verification still writing
      nothing

## 3. Saying it

- [ ] 3.1 The Connector form states the required permissions for the current configuration
- [ ] 3.2 The credential-test panel (#132) renders the not-verifiable outcome as its own state
- [ ] 3.3 `SELF-HOSTING.md` carries the same list, generated from or checked against the same set
- [ ] 3.4 i18n entries; no hardcoded copy

## 4. The decision

- [ ] 4.1 A DEC recording that scope breadth follows configuration (revising DEC-030) and that an
      unverifiable capability is reported rather than assumed

## 5. Proof

- [ ] 5.1 Browser-preview verification of both configurations' stated permissions and of the
      third verdict state
- [ ] 5.2 Full gates — build, tests, lint, spec validation, design validator — plus the #220
      retro's grep of the e2e tier for any label this touches
