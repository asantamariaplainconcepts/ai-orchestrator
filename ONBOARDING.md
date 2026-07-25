# Onboarding

Zero to your first contribution. Setup commands live in [`README.md`](README.md) — not repeated here.

## 1. Get oriented (~30 min)

- **What we're building** — [product brief](docs/product/mvp/00-product-brief.md), then the
  [glossary](docs/product/mvp/02-domain-glossary.md). The coined words are load-bearing:
  *Agent* (never "pod"), *Connector*, *Automation*, *Run*, *Plan*.
- **How it's built** — [`ARCHITECTURE.md`](ARCHITECTURE.md): the modular monolith, its enforced
  seams, and the two distinct error channels.
- **How work is shaped** — the [Definition of Ready](docs/process/definition-of-ready.md), built
  on the [backlog rules](docs/product/mvp/08-backlog-shaping-rules.md).

## 2. Get it running

Follow the quick start in [`README.md`](README.md). If a step fails, **file it** — onboarding
friction is a defect, not a rite of passage.

## 3. First contribution — full loop in [`CONTRIBUTING.md`](CONTRIBUTING.md)

1. [`/aio:grill`](.claude/commands/aio/grill.md) — an idea or use case reaches the Definition of
   Ready and becomes an issue.
2. [`/aio:propose`](.claude/commands/aio/propose.md) — the spec lands on a **draft PR** and is
   reviewed *before any code exists*. Your design gets shaped here, cheaply.
3. [`/aio:implement`](.claude/commands/aio/implement.md) — same branch, same PR, then code review.
4. [`/aio:sync`](.claude/commands/aio/sync.md) — the only merge path.

Lost the thread? [`/aio:status`](.claude/commands/aio/status.md). Pair with someone your first time.

## 4. Guardrails that will otherwise surprise you

Warnings are build errors and style is compiler-enforced, so formatting is never a review topic.
Cross-module access goes through `.Contracts` only. Hardcoded user-facing text fails lint. Never
skip the two review gates or merge outside `/aio:sync`. And a claim is unverified until something
exercises it ([ADR-0001](docs/adr/0001-verify-claims-by-exercising-them.md)).

## Help

Agents enter at [`AGENTS.md`](AGENTS.md) · past decisions → [`docs/adr/`](docs/adr/README.md).
