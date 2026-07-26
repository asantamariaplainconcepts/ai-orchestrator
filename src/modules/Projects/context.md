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

## Automations, and why a save can be refused

BC-001 owns Automations and their validation, so they live here rather than in a module of their
own. An Automation is a trigger (label, optionally a Story state), an action from DEC-026's
catalogue, a runtime, `requiresApproval`, and a timeout.

**BR-003 rejects overlapping triggers at save time** (DEC-033), and "overlap" means *some Story
could match both*:

| A | B | Overlap |
|---|---|---|
| label `L`, state `S` | label `L`, state `S` | yes |
| label `L`, state `S1` | label `L`, state `S2` | no |
| label `L`, **any** state | label `L`, state `S` | **yes** — subsumption |
| label `L1` | label `L2` | no |

Disabled Automations are ignored. The rule is symmetric, so **whichever is saved second is
refused** — that is the price of validating at write time instead of resolving priority at read
time, and it is what an Admin actually experiences.

**Do not replace this with a unique index.** `(ProjectId, TriggerLabel, TriggerState)` catches
exact duplicates and silently permits the subsumption case, which is the one that matters.

**Known debt:** two concurrent creates can both pass the check and both commit — the rule is not
expressible as a constraint. Accepted because Automations are configured by one Admin at human
pace; it needs a real answer the day that stops being true.

**Public surface:** `ProjectsModule` only. Everything else is `internal` — enforced by MOD001,
MOD003, MOD005 and CQS001. Tests reach internals through `InternalsVisibleTo`, not by widening
visibility.
