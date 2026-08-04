## Why

Issue #250. The AppHost's two declaration sets are keyed to `IsRunMode`/`IsPublishMode`:
`aspire run` *is* the dev loop, and the server shape only exists published. A developer who wants
to see what an operator gets — pods on, Local locus declared out, no seeder — has to build images
and `docker compose up`. And the if/else carrying those sets has grown three changes in a week
(#225, #247, #246); each addition edits a branch of an anonymous conditional rather than a named
declaration block.

## What Changes

- An Aspire **parameter** `habitat` (`Parameters:habitat`, user-secrets/appsettings overridable,
  default `local`) picks which declaration set run mode applies:
  - `local` → today's dev loop exactly: seeder, LocalOwner, local secret store, Local locus
    available, in-process execution.
  - `server` → the compose declarations under `aspire run`: pods, locus reason, no seeder.
- An unknown value refuses at startup naming the two valid ones.
- The AppHost reorganised into **one method per declaration set**, applied to the server from one
  place — the two shapes diffable side by side, publish output byte-identical.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dev-orchestration`: the run-mode habitat is a parameter with a default, and the declaration
  sets are named blocks.

## Impact

- `src/root/AiOrchestrator.AppHost/AppHost.cs` only (plus regenerated compose proving
  byte-identity and a CONTRIBUTING line). No product code: the Server keeps reading
  declarations, never modes (ADR-0010).
