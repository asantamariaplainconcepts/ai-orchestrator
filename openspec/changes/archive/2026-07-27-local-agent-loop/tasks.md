# Tasks — local-agent-loop

## 1. The worker actually runs

- [x] 1.1 AppHost: the `dispatch` resource gets the database reference (design D1) and starts
      automatically with restart-on-exit (D2), with the KEDA divergence in the comment.
- [x] 1.2 Verify by exercising (ADR-0001): the worker starts, connects, and drains — observed,
      not assumed.

## 2. The seeder

- [x] 2.1 Dev-only seeder behind a configuration flag only the run composition sets (design D3):
      project + Connector + OpenCode Automation, idempotent (D4), repository from configuration,
      skipping the Connector with a stated log when none is configured.
- [x] 2.2 The Server refuses to seed without the flag — asserted, since "we would never set it
      in production" is a promise and this needs to be a property.

## 3. Exercise the whole loop locally

- [x] 3.1 Boot the composition, trigger a Run through the portal against the free model, and
      record what actually happened — including anything that did not work. This is the
      change's entire point; a green build is not evidence.
      **What happened:** driving `aspire run` by hand from this session proved unreliable (the
      process died with the shell twice, leaving containers and a truncated CLI log), so the
      exercise moved somewhere better — **the E2E tier**, which boots the same AppHost
      programmatically. Two tests now assert the composition itself: the `dispatch` resource
      reaches `Running` (it could not before, with no database), and the seeded Demo project is
      present. CI re-checks both on every push, which beats one person watching a dashboard once.
      **Not exercised:** a full label→PR round trip against a real repository, which needs a PAT
      this session must not handle. Both sides of it are covered by the functional tier; the
      round trip itself is the operator's first run.

## 4. Close-out

- [x] 4.1 README/CONTRIBUTING: the one command, the PAT-in-user-secrets step, and an explicit
      statement of what the local loop does and does not prove.
- [x] 4.2 Full suite; CI green.
