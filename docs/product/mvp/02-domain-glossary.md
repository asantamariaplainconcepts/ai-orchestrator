# Domain glossary — the ubiquitous language

One term, one meaning. Coined terms were locked in the charter ([DEC-005](10-locked-mvp-decisions.md));
renaming any of these is a BREAKING product change once built upon.

| Term | Meaning |
|---|---|
| **Project** | The orchestrator's unit of configuration: one Connector, its credential reference, its Automations, its caps and members. Created and owned in the website ([UC-001](04-mvp-use-cases.md)). |
| **Connector** | The vendor abstraction over issue backlogs. MVP implementations: GitHub, Azure DevOps ([DEC-011](10-locked-mvp-decisions.md)). A Project has exactly one. |
| **Story** | A user story / work item read through the Connector. Lives in the vendor (source of truth, [BR-008](05-business-rules.md)); the orchestrator holds a cached mirror ([DEC-029](10-locked-mvp-decisions.md)). |
| **Trigger label** | The vendor-side label (GitHub label / AzDO tag) whose presence on a Story matches an Automation. Applicable from the website or in the vendor tool — same semantics ([DEC-027](10-locked-mvp-decisions.md)). |
| **Automation** | The product's core noun: a configured mapping "Story with trigger label/state X → Agent performs action Y", with settings (`action`, `runtime`, `requiresApproval`, `timeout`). Overlapping triggers are rejected at save ([BR-003](05-business-rules.md)). |
| **Catalogue** | Every Automation a Project has — its inventory of what it can do. Distinct from the Workflow, which is only the Automations that hand work to one another ([DEC-053](10-locked-mvp-decisions.md)). |
| **Workflow** | The path formed by Automations that hand work on: an Automation is in it exactly when it hands work to another or another hands work to it. Derived from those edges and never stored. An Automation outside it is not a special case of it — it is a trigger that acts on its own ([DEC-053](10-locked-mvp-decisions.md)). |
| **Action** | What an Automation instructs the Agent to do. MVP catalog ([DEC-026](10-locked-mvp-decisions.md)): *Implement→PR*, *Refine/comment*, *Transition state*, *Estimate*. |
| **Agent** | The AI execution unit: a Runtime executing in a per-Run **sandbox** — a microVM with its own kernel, created for one Run and gone with it ([DEC-013](10-locked-mvp-decisions.md), superseded; the sandbox substrates are sbx locally and Azure Container Apps Sandboxes in a deployment). Never "pod". |
| **Runtime** | The pluggable image an Agent runs. MVP: Claude Code headless, opencode ([DEC-012](10-locked-mvp-decisions.md)). Selected per Automation. |
| **Run** | One execution of an Automation against one Story: lifecycle, plan, logs, output link, cost. States: `Queued · Planning · AwaitingApproval · Executing · Succeeded · Failed · Cancelled` ([BR-014](05-business-rules.md)). |
| **Plan** | The proposal an Agent produces in phase 1 of an approval-gated Run — reviewed by a human in the website before execution, mirroring spec review ([DEC-040](10-locked-mvp-decisions.md)). |
| **Approval** | The human act of marking a Plan ready for execution ([UC-011](04-mvp-use-cases.md)). Only exists when the Automation has `requiresApproval` ([DEC-039](10-locked-mvp-decisions.md)). |
| **Dispatch** | The orchestrator enqueueing a Run message on the Azure Storage Queue that KEDA watches ([DEC-013](10-locked-mvp-decisions.md)). |
| **Mirror** | The Postgres read model of Stories (id, title, state, labels, last-seen), refreshed by polling and webhooks ([DEC-028](10-locked-mvp-decisions.md), [DEC-029](10-locked-mvp-decisions.md)). |
| **Run now** | Manually enqueueing a chosen Automation against a chosen Story, bypassing detection but honoring all business rules ([BR-013](05-business-rules.md)). |
