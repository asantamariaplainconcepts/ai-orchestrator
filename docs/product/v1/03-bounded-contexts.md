# Bounded contexts

Domain contexts — explicitly **not** technical modules. The locked module vocabulary is
Projects / Backlog / Agents ([DEC-007](../mvp/10-locked-mvp-decisions.md)); whether a context
becomes its own backend module is decided at real seams only.

| ID | Context | Owns | Key language |
|---|---|---|---|
| BC-001 | **Project Configuration** | Projects, Connector config (vendor + code source), credential references (names, never values — the habitat's store holds them, [BR-010](05-business-rules.md)), Automations and their validation (overlap rejection), caps | Project, Connector, Automation, Action, Runtime, Code source |
| BC-002 | **Backlog Mirror** | The Story read model; polling + webhook ingestion normalized into one event stream; label write-back through the Connector | Story, Trigger label, Mirror |
| BC-003 | **Dispatch & Approval** | Matching normalized story events to Automations, creating Runs, enforcing concurrency rules, the approval pause, publishing to the outbox | Run, Plan, Approval, Dispatch |
| BC-004 | **Agent Execution** | The Run lifecycle from the sandbox's perspective: runtime images, sandbox creation and teardown, phase execution, log/transcript capture, output links, usage/cost reporting, conversations and attached sessions | Agent, Runtime, Sandbox, Run, Plan, Conversation, Transcript |
| BC-005 | **Identity & Access** | Sign-in (Entra ID as BFF, [DEC-058](../mvp/10-locked-mvp-decisions.md)), the permission catalog, the two fixed role bundles | Permission, Role (Admin, Member) |

Context relationships: BC-002 feeds BC-003 (normalized events); BC-003 commands BC-004
through the Postgres outbox ([DEC-013](../mvp/10-locked-mvp-decisions.md) superseded);
BC-004 reports back into BC-003 (run state, cost); BC-001 configures all of them; BC-005
guards every human entry point.
