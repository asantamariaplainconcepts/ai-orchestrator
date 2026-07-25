<!-- Thanks for contributing to AI Orchestrator. Fill this in so review can move fast. -->

## Summary

<!-- What does this change do, and why? -->

Closes #

## Spec / decision

<!-- Spec-first is enforced. Link the OpenSpec change (openspec/changes/<name>/) or the ADR.
     Spec-less lane (hotfix / infra, DEC-025)? Say so here and label the issue lane:spec-less. -->

- OpenSpec change / ADR:
- Product IDs addressed (UC / BR / DEC):

## Type of change

- [ ] feat
- [ ] fix
- [ ] docs
- [ ] refactor / chore

## Module / area

<!-- e.g. Projects, Backlog, Agents, Host (AiOrchestrator.Server), Shared/BuildingBlocks, Frontend, Infra/CI -->

Module:

## Time invested

<!-- Tracked deliberately: telemetry cannot see human time, so it is recorded here or nowhere. -->

- Human time (grill answers, prompting, review, steering):
- Agent/session time:
- Estimated time without AI assistance (optional, for comparison):

## Definition of Done

- [ ] OpenSpec change updated (proposal/design/specs/tasks), or an ADR exists, or the spec-less lane is declared above.
- [ ] EF Core migration added if a module's schema changed, and it applies cleanly.
- [ ] `dotnet test src/AiOrchestrator.slnx` passes (unit + functional + arch).
- [ ] `dotnet csharpier check src` passes.
- [ ] `pnpm format:check`, `pnpm lint`, `pnpm typecheck` pass (run from `src/frontend`).
- [ ] New endpoints have a FluentValidation validator and return `ErrorOr<T>` mapped via `ApiResults.Problem`.
- [ ] Cross-module access goes through `.Contracts` only (MOD001–005 clean).
- [ ] All user-facing copy comes from the typed i18n catalog (no hardcoded JSX text).
- [ ] Commit messages follow Conventional Commits.

## Human-in-the-loop

<!-- Tick any that apply; these warrant an extra-careful review. -->

- [ ] Adds or changes an EF Core migration.
- [ ] Adds a module, external integration, or top-level dependency.
- [ ] Touches module boundaries (`.Contracts`, `AiOrchestrator.ArchitectureAnalyzers`, `ArchTests`).
- [ ] Touches CI, `infra/`, or commit-hook config (`.config/`, `.husky/`).
- [ ] Touches host routing (reserved-prefix list / SPA fallback).
- [ ] Touches secrets handling or Key Vault references.
- [ ] None of the above.

## Screenshots / notes

<!-- For frontend changes, include before/after screenshots. -->
