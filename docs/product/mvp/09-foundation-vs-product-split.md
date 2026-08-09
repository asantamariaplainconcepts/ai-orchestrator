# Foundation vs product split

Enabling infrastructure vs user-visible capability. Every issue is classified as one
([RULE-005](08-backlog-shaping-rules.md)); foundation work is sequenced deliberately,
never smuggled into feature items.

## Foundation (enables, invisible to users)

| Item | Enables | Notes |
|---|---|---|
| Repo scaffolding (modular monolith, build props, analyzers, test bases, CI/CD) | everything | Phase 1 of the bootstrap |
| Auth foundation (Entra ID integration, permission model plumbing) | UC-001, UC-002, BR-009 | Gated by [OPN-002](07-open-decisions.md) |
| Connector seam (normalized story events, write-back contract) | all of BC-002 | Seam designed once; GitHub implements it first (DEC-011) |
| Postgres outbox dispatch + ACA SandboxGroups + Key Vault wiring | all Runs | One dispatch substrate in every habitat since DEC-013's supersession — the outbox integration events already use. The deployed difference is where the Agent executes, not how a Run is dispatched |
| Runtime seam (job contract: story prompt in, plan/output/usage out) | UC-015..020 | Claude Code headless implements it first (DEC-012) |
| Telemetry: OTel ServiceDefaults + Azure Monitor exporters (product), usage-telemetry stack (framework) | UC-020, retros | DEC-022/023 |
| Design system (Atlas-style tokens, DESIGN.md, drift gate) | all UI | Bootstrap Phase 4 |
| Frontend skeleton (Vite React same-origin, slices, i18n catalog + gate) | all UI | DEC-009 |

## Product (user-visible capabilities)

Everything cataloged in [04-mvp-use-cases.md](04-mvp-use-cases.md): project/connector/
automation configuration, backlog viewing and labeling, run creation and approval,
the four Agent actions, run observation with logs and cost.

## Sequencing spine

Auth → scaffolded backlog mirror (GitHub) → Automations config → dispatch (queue +
one-phase runs) → first Agent action end-to-end (Implement→PR, Claude Code) →
approval gate (two-phase) → remaining actions → cancellation + cost → AzDO connector →
opencode runtime → webhooks (polling ships first).
