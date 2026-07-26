# Projects module — context

**Bounded context:** BC-001 Project Configuration.

**Owns:** the Project aggregate and, as the product grows, its Automations and caps. Today it
holds only what the exemplar slices need — a project's name.

**Does not own the Connector.** Configuring one is a Backlog concern, so it lives in
[the Backlog module](../Backlog/context.md) keyed by a plain `ProjectId` — see that file for the
reasoning and the debt it carries.

**Schema:** `projects` (own `DbContext`, own migrations).

**Slices:**

| Use case | Route | Product ID |
|---|---|---|
| [CreateProject](AiOrchestrator.Modules.Projects/Features/Projects/UseCases/CreateProject.cs) | `POST /api/projects` | UC-003 |
| [ListProjects](AiOrchestrator.Modules.Projects/Features/Projects/UseCases/ListProjects.cs) | `GET /api/projects` | UC-007 (project side) |

**Why this module exists first:** it is the reference implementation every later module copies —
the command slice, the query slice, the `{Entity}Errors` type, the schema-per-module wiring, and
the test shape (unit for pure logic, functional against real containers). Modules are drawn at
real seams only, so nothing is scaffolded empty; `Backlog` arrived when a change needed it, and
`Agents` still does not exist.

**Public surface:** `ProjectsModule` only. Everything else is `internal` — enforced by MOD001,
MOD003, MOD005 and CQS001. Tests reach internals through `InternalsVisibleTo`, not by widening
visibility.
