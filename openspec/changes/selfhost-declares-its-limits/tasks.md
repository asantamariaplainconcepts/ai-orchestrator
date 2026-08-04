## 1. The declared fact

- [ ] 1.1 Configuration key (`Habitat:LocalFolderUnavailableReason`) read in one place; the
      capabilities response gains `CanUseLocalFolder` + `LocalFolderReason` (design D2)
- [ ] 1.2 The AppHost publish composition sets the reason on the Server; regenerate
      `selfhost/docker-compose.yaml` (design D1, the #225 drift lesson)
- [ ] 1.3 Functional tests: declared → withheld with reason; undeclared → offered as today

## 2. The two doors

- [ ] 2.1 `ConfigureConnector` refuses a `LocalFolder` save in a declaring habitat, naming the
      reason (design D3)
- [ ] 2.2 `RunCreator` refuses a Local-locus resolution in a declaring habitat, naming the reason
- [ ] 2.3 Functional tests: the save refusal, the pre-existing-Connector Run refusal, and the
      dev-loop unchanged path

## 3. The surface

- [ ] 3.1 The code-source section reads the new capability; the local option is withheld and the
      reason shown in its place; i18n entries (aio-design skill)

## 4. Docs and proof

- [ ] 4.1 DEC-049 self-host docs state what compose self-host cannot do and why, and that an
      operator who mounts and unsets the declaration owns the consequence
- [ ] 4.2 Full gates: build, tests, lint, spec validation, design validator, compose-drift
