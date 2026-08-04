## 1. The parameter and the blocks

- [x] 1.1 Read `Parameters:habitat` in run mode, default `local`; refuse unknown values naming
      both valid ones (design D1/D3)
- [x] 1.2 Extract `DeclareDevLoop` / `DeclareServerShape` / shared run-mode ergonomics as named
      methods (design D2); publish keeps calling the server set unchanged

## 2. Proof

- [x] 2.1 Regenerate the compose: byte-identical (design D4)
- [x] 2.2 Exercised for real: `aspire run` (or the AppHost model in a test) with habitat=server
      carries the compose declarations; default carries the dev loop's; unknown refuses
- [x] 2.3 CONTRIBUTING line: how to switch habitats locally
- [x] 2.4 Full gates — build, tests, lint, spec validation, compose-drift
