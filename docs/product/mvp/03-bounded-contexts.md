# Bounded contexts

Domain contexts — explicitly **not** technical modules. The locked module vocabulary is
Projects / Backlog / Agents ([DEC-007](10-locked-mvp-decisions.md)); whether a context
becomes its own backend module is decided at scaffolding time, at real seams only.

| ID | Context | Owns | Key language |
|---|---|---|---|
| BC-001 | **Project Configuration** | Projects, Connector config, credential references (Key Vault names, never values), Automations and their validation (overlap rejection), caps | Project, Connector, Automation, Action, Runtime |
| BC-002 | **Backlog Mirror** | The Story read model; polling + webhook ingestion normalized into one event stream; label write-back through the Connector | Story, Trigger label, Mirror |
| BC-003 | **Dispatch & Approval** | Matching normalized story events to Automations, creating Runs, enforcing concurrency rules, the approval pause, enqueueing to the Storage Queue | Run, Plan, Approval, Dispatch |
| BC-004 | **Agent Execution** | The Run lifecycle from the job's perspective: runtime images, phase execution, log capture, output links, usage/cost reporting at run end | Agent, Runtime, Run, Plan |
| BC-005 | **Identity & Access** | Sign-in (Entra ID, [DEC-024](10-locked-mvp-decisions.md)), the permission catalog, the two fixed MVP role bundles | Permission, Role (Admin, Member) |

Context relationships: BC-002 feeds BC-003 (normalized events); BC-003 commands BC-004
(queue messages); BC-004 reports back into BC-003 (run state, cost); BC-001 configures
all of them; BC-005 guards every human entry point.
