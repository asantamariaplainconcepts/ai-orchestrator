## Context

Three changes in one week each added a declaration to one branch of the AppHost's run/publish
conditional. The publish branch *is* the server shape; run mode has no way to rehearse it. Aspire
parameters (`builder.AddParameter` / `Parameters:habitat` in configuration) are the idiomatic
switch: user-secrets overridable, visible in the dashboard, no environment variables invented.

## Goals / Non-Goals

**Goals:**
- `aspire run` can rehearse the server shape without docker compose.
- Each declaration set is one named method; adding a declaration is one edit in one place.
- Publish output stays byte-identical — this change moves code, not behaviour.

**Non-Goals:**
- Changing any declaration (owned by #225/#246/#247).
- Real authentication in a local `server` habitat.
- Touching ACA/Terraform.

## Decisions

**D1 — a plain configuration value, read at composition time.** `Parameters:habitat` is read
with `builder.Configuration["Parameters:habitat"]`, defaulting to `local`. Deliberately NOT
`builder.AddParameter(...)`: an Aspire parameter *resource* materialises as a deployment
parameter in publish output, and this value must never reach the artifact — publish mode ignores
it entirely and always emits the server declarations. Reading the same `Parameters:` section
keeps the user-secrets ergonomics (`dotnet user-secrets set Parameters:habitat server`) without
the publish side effect.

**D2 — three named blocks: `DeclareDevLoop`, `DeclareServerShape`, and what both share.**
Run+local → dev loop; run+server → server shape plus the run-mode ergonomics that are about
*running locally* rather than about the habitat (ASPNETCORE_ENVIRONMENT, the Vite proxy);
publish → server shape, unchanged. The seeder is a dev-loop declaration, not a run-mode one —
rehearsing the operator's shape means seeing the empty first boot they see.

**D3 — unknown values refuse at startup, naming both valid ones.** The queue/outbox rule
(DEC-054): ambiguity refuses where a person is watching, never defaults silently.

**D4 — byte-identity is the proof of the refactor.** `./scripts/generate-compose.sh` before and
after must produce no diff; CI's drift gate holds it thereafter.

## Risks / Trade-offs

- [`server` under `aspire run` points pods at `selfhost_aspire`, a network that does not exist
  there] → acceptable and honest: the Run fails naming docker/network state, which is the
  rehearsal working — the operator's failure modes are visible too. The rehearsal is of the
  *declarations*, not of a working end-to-end (that needs the image built either way).
- [A fourth habitat someday] → it is one more named method and one more valid value in the
  refusal sentence.
