# Design: self-host-distribution

## D1 — One composition, three habitats

`aspire run`, the generated compose, and ACA all describe the same system because two of them
share one source (the AppHost) and the third is Terraform that reuses the same images'
Dockerfiles. The publish-mode forks are exactly two, both stated in the AppHost where they
happen, both existing because a dev-only resource (Vite) and a cloud resource (Azure Storage)
have no compose shape of their own.

## D2 — The artifact is committed AND regenerated

Committing the generated compose makes `git clone && docker compose up --build` work without
any .NET tooling. Committing a generated file invites drift, so CI regenerates and diffs — the
same treatment every derived artifact gets here (ADR-0003). Determinism made that check honest:
the postgres volume is named explicitly because the default embeds a path hash that differs per
checkout.

## D3 — The worker is a timer-driven drainer, stated

No KEDA in compose: the dispatch service gets `Dispatch__LocalPollSeconds`, the same divergence
the local loop documents — WHAT starts a pass differs, the pass is byte-identical. What compose
proves about the queue contract is real; what it proves about scaling is nothing.
