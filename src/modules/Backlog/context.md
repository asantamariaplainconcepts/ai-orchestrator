# Backlog module — context

**Bounded context:** BC-002 Backlog Mirroring.

**Owns:** the Connector (which repository a Project reads, and under which credential name), and
the mirrored Stories.

**Schema:** `backlog` (own `DbContext`, own migrations).

**Slices:**

| Use case | Route | Product ID |
|---|---|---|
| [ConfigureConnector](AiOrchestrator.Modules.Backlog/Features/Backlog/UseCases/ConfigureConnector.cs) | `PUT /api/projects/{projectId}/connector` | UC-004 |
| [GetBacklog](AiOrchestrator.Modules.Backlog/Features/Backlog/UseCases/GetBacklog.cs) | `GET /api/projects/{projectId}/backlog` | UC-007 |
| [RefreshBacklog](AiOrchestrator.Modules.Backlog/Features/Backlog/UseCases/RefreshBacklog.cs) | `POST /api/projects/{projectId}/backlog/refresh` | UC-009 |

## Why the Connector lives here and not in Projects (design D2)

A Connector is configuration *of a Project*, so the obvious home is the Projects module. It is
here instead because everything that reads or writes one is a Backlog concern: verifying it
against the vendor, polling with it, recording why the last poll failed. Putting it in Projects
would mean Projects owning a row that only Backlog ever interprets, and every poll crossing a
module boundary to fetch it.

**This is a real trade, not a free win.** `Connector.ProjectId` is a plain `Guid` with no foreign
key, because a cross-schema constraint is exactly the coupling the module boundary exists to
prevent. So the database will not stop a Connector outliving its Project.

**The debt:** deleting a Project must also delete its Connector and Stories, and nothing does
that today because nothing deletes Projects yet. When project deletion is specified, it needs a
deliberate answer — most likely a domain event Backlog subscribes to — and not a foreign key
added in a migration, which would silently re-couple the schemas. Whoever implements deletion
should read this paragraph first.

## The vendor seam

`IBacklogConnector` is the only thing the rest of the module knows about a vendor. Octokit is
confined to `GitHubBacklogConnector`, which translates vendor failures into the module's closed
error set — keeping "wrong repository" and "wrong credential" apart, because the two have
different fixes. A second vendor registers alongside and nothing else changes.

`Backlog:GitHub:BaseAddress` points the client at a GitHub Enterprise Server instance. The E2E
lane uses the same knob to put a stub in front of the real Octokit client, which is why that tier
needs no GitHub token.

## Credentials

Only the *name* of a secret is ever stored, logged, or returned (BR-010). `ISecretResolver`
turns a name into a token at the moment of use, and the host decides where names resolve from.

**Public surface:** `BacklogModule` only. Everything else is `internal`.
