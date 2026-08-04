## 1. The migration

- [ ] 1.1 Server: build context + `${SERVER_PORT}` mapping via its own
      `PublishAsDockerComposeService`; migrations: build context (design D1/D2)
- [ ] 1.2 Postgres: database env + healthcheck on the resource; `service_healthy` conditions on
      each dependent (design D1)
- [ ] 1.3 Delete `ConfigureComposeFile` and the dead `dispatch` entries; regenerate the compose
      (equivalent, not identical)

## 2. Proof

- [ ] 2.1 Real boot: fresh volumes, `docker compose up`, postgres healthy → migrations complete →
      server answers on SERVER_PORT; observations recorded (design D3)
- [ ] 2.2 Full gates — build, tests, lint, spec validation, compose-drift — plus a grep for any
      name this change retires
