## 1. The migration

- [x] 1.1 Server: build context + `${SERVER_PORT}` mapping via its own
      `PublishAsDockerComposeService`; migrations: build context (design D1/D2)
- [x] 1.2 Postgres: database env + healthcheck on the resource; `service_healthy` conditions on
      each dependent (design D1)
- [x] 1.3 Delete `ConfigureComposeFile` and the dead `dispatch` entries; regenerate the compose
      (equivalent, not identical — came out byte-identical)

## 2. Proof

- [x] 2.1 Real boot: fresh volumes, `docker compose up`, postgres healthy → migrations complete →
      server answers on SERVER_PORT; observations recorded (design D3). Observed: no prior
      `selfhost_aio-postgres-data` volume; compose reported postgres Waiting → Healthy,
      migrations exited 0, server started only after both conditions; HTTP 200 on
      `localhost:8080` (SPA served) 0.1s after start. Incidental finds: the repo-root
      `.dockerignore` (1.3GB context otherwise), and a machine-local hang in
      `docker-credential-desktop` that stalled every image pull — not a property of this change.
- [x] 2.2 Full gates — build, tests, lint, spec validation, compose-drift — plus a grep for any
      name this change retires. All green: Release build, 496/496 tests (incl. 43 E2E),
      CSharpier, Prettier/ESLint/tsc, design-system, `openspec validate`, drift regeneration
      byte-identical; `ConfigureComposeFile` survives only inside Aspire's own binaries and the
      remaining `Dispatch__*` entries are the server's env, not compose patches.
