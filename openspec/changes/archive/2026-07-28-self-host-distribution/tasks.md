# Tasks — self-host-distribution

- [x] 1.1 Compose environment in the AppHost: build contexts instead of image placeholders,
      fixed server port mapping, deterministic volume name; the two publish-mode forks (D1).
- [x] 2.1 `selfhost/docker-compose.yaml` generated and committed; `scripts/generate-compose.sh`;
      the CI drift job (D2).
- [x] 3.1 SELF-HOSTING.md + README pointer; `.env.example`.
- [x] 4.1 Exercised: the generated compose boots on this machine with `up --build` and the
      portal answers — recorded in the PR with date and outcome (ADR-0001/0005).
