## 1. The parameter and the blocks

- [ ] 1.1 Read `Parameters:habitat` in run mode, default `local`; refuse unknown values naming
      both valid ones (design D1/D3)
- [ ] 1.2 Extract `DeclareDevLoop` / `DeclareServerShape` / shared run-mode ergonomics as named
      methods (design D2); publish keeps calling the server set unchanged

## 2. Proof

- [ ] 2.1 Regenerate the compose: byte-identical (design D4)
- [ ] 2.2 Exercised for real: `aspire run` (or the AppHost model in a test) with habitat=server
      carries the compose declarations; default carries the dev loop's; unknown refuses
- [ ] 2.3 CONTRIBUTING line: how to switch habitats locally
- [ ] 2.4 Full gates — build, tests, lint, spec validation, compose-drift
