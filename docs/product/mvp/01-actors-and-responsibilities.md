# Actors and responsibilities

Authorization is **permission-based**: every operation requires a named permission;
roles are permission bundles ([DEC-034](10-locked-mvp-decisions.md), [BR-009](05-business-rules.md)).
MVP ships exactly two fixed human roles; custom roles are post-MVP.

| ID | Actor | Kind | May |
|---|---|---|---|
| ACT-001 | **Admin** | Human role | Everything: create projects, configure Connectors and credential references, manage Automations (incl. `requiresApproval`, timeout, caps), assign roles — plus everything a Member may. |
| ACT-002 | **Member** | Human role | View backlog, runs, logs and cost; apply/remove trigger labels on stories ([UC-007](04-mvp-use-cases.md)); trigger *Run now* ([UC-012](04-mvp-use-cases.md)); approve plans ([UC-011](04-mvp-use-cases.md)); cancel runs ([UC-019](04-mvp-use-cases.md)). May **not** create or edit project config or Automations. |
| ACT-003 | **Agent** | System actor | The AI execution unit — a Runtime executing in a per-Run **sandbox** (a microVM with its own kernel: `sbx` locally, an ACA Sandbox in a deployment), or as a child of the portal's own process where no launcher is configured. Reads a story through the Connector, produces a plan (phase 1) and/or executes the configured action (phase 2): open PRs, comment, transition, estimate. Acts only inside a Run created by the orchestrator. |
| ACT-004 | **Backlog Vendor** | External system | GitHub / Azure DevOps. Source of truth for stories ([BR-008](05-business-rules.md)). Pushes webhook events; answers Connector reads/writes. |

Product authority for all open questions: the repo owner, solo ([DEC-003](10-locked-mvp-decisions.md)).
