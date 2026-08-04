## Context

`AddDockerComposeEnvironment(...).ConfigureComposeFile(...)` rewrites the serialized model:
image→build swaps keyed by service name, a ports array, postgres env + healthcheck, and
depends_on conditions — each discovered by booting the output (#99). Aspire 13.4 exposes the
same service nodes per resource: `PublishAsDockerComposeService` hands each resource its own
`Service` before serialization, so the facts can live beside the resources they describe.

## Goals / Non-Goals

**Goals:**
- Zero `ConfigureComposeFile`; every fact on its resource; dead entries gone.
- The operator's quickstart unchanged: same `.env` variables, same `docker compose up`.

**Non-Goals:**
- Changing what the compose contains.
- Touching the habitat declarations or the Azure path.
- Upgrading Aspire.

## Decisions

**D1 — one `PublishAsDockerComposeService` per resource, beside the resource.** The server
declares its build and port; migrations declares its build; postgres declares its database,
healthcheck, and nothing else — and each dependent declares its own `service_healthy` condition,
because "wait until postgres is healthy" is the dependent's requirement, not postgres's.

**D2 — the port stays `${SERVER_PORT}:${SERVER_PORT}`, written through the service node.** The
quickstart says "open localhost:$SERVER_PORT" and a random host port would turn it into a
scavenger hunt (#99's reason, unchanged). If the callback cannot express the literal placeholder,
the fallback is `ConfigureEnvFile` + `AsEnvironmentPlaceholder` — never a revived global block.

**D3 — equivalence is proven by booting, not by diffing.** The output's shape may change; what
must hold is: fresh volumes, postgres healthy, migrations complete, server answers. #99 found
both of its bugs only at boot, and this change edits exactly the mechanics #99 patched.

**D4 — a fact with no per-resource idiom moves to the operator's layer, not back to a global
block.** Grilled decision: zero `ConfigureComposeFile`. If something truly cannot be declared
per resource, it becomes a documented line in the README's override example — the operator's
file is the escape hatch this repo already uses for the docker socket (#246).

## Risks / Trade-offs

- [The Docker.Resources.ServiceNodes API shape differs from the file-level one] → the same types
  are used by both callbacks; the risk is mostly discovery cost, bounded by the boot proof.
- [Regenerating the drift baseline hides an accidental behaviour change] → that is what D3's
  real boot is for; the diff is also read by hand once at review.
- [`service_healthy` on migrations AND server] → the server also waits for migrations
  completion; both conditions must survive, and the boot (fresh volumes) exercises exactly that
  ordering.

## Migration Plan

One change: move the declarations, delete the block, regenerate, boot, commit the new baseline.
Rollback is `git revert` of one commit — the artifact is generated either way.

## Open Questions

(none)
