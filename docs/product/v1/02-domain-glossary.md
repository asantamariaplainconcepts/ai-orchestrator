# Domain glossary — the ubiquitous language

One term, one meaning. Coined terms were locked in the charter
([DEC-005](../mvp/10-locked-mvp-decisions.md)); renaming any of these is a BREAKING product
change once built upon. Terms marked *(new in v1)* were load-bearing in decisions before this
glossary learned them.

| Term | Meaning |
|---|---|
| **Project** | The orchestrator's unit of configuration: one Connector, its credential reference, its Automations, its caps and members. Created and owned in the website ([UC-003](04-capabilities.md)). |
| **Connector** | The vendor abstraction over issue backlogs. Implementations: GitHub, Azure DevOps ([DEC-011](../mvp/10-locked-mvp-decisions.md)). A Project has exactly one. Since #210 it also carries the **code source**. |
| **Story** | A user story / work item read through the Connector. Lives in the vendor (source of truth, [BR-008](05-business-rules.md)); the orchestrator holds a cached mirror ([DEC-029](../mvp/10-locked-mvp-decisions.md)). |
| **Trigger label** | The vendor-side label (GitHub label / AzDO tag) whose presence on a Story matches an Automation. Applicable from the website or in the vendor tool — same semantics ([DEC-027](../mvp/10-locked-mvp-decisions.md)). |
| **Automation** | The product's core noun: a configured mapping "Story with trigger label/state X → Agent performs action Y", with settings (`action`, `runtime`, `requiresApproval`, `timeout`). Overlapping triggers are rejected at save ([BR-003](05-business-rules.md)). |
| **Catalogue** | Every Automation a Project has — its inventory of what it can do. Distinct from the Workflow ([DEC-053](../mvp/10-locked-mvp-decisions.md)). |
| **Workflow** | The path formed by Automations that hand work on: an Automation is in it exactly when it hands work to another or another hands work to it. Derived from those edges and never stored ([DEC-053](../mvp/10-locked-mvp-decisions.md)). |
| **Action** | What an Automation instructs the Agent to do. Catalog ([DEC-026](../mvp/10-locked-mvp-decisions.md), extended by [DEC-057](../mvp/10-locked-mvp-decisions.md)): *Implement→PR*, *Refine/comment*, *Transition state*, *Estimate*, and a project-written prompt. |
| **Agent** | The AI execution unit: a Runtime executing in a per-Run sandbox. Never "pod". |
| **Runtime** | The pluggable image an Agent runs: Claude Code headless, opencode ([DEC-012](../mvp/10-locked-mvp-decisions.md)). Selected per Automation. |
| **Sandbox** *(new in v1)* | The per-Run isolation unit: a microVM with its own kernel, created for one Run and gone with it. Substrates: `sbx` on a local machine, Azure Container Apps Sandboxes in a deployment ([DEC-013](../mvp/10-locked-mvp-decisions.md) superseded, #296). A conversation's sandbox stays warm while the conversation lives ([DEC-061](../mvp/10-locked-mvp-decisions.md)). |
| **Habitat** *(new in v1)* | Where an instance of the product lives: a **deployment** (metered infrastructure someone else pays for) or **self-host** (a machine its operator owns); the `aspire run` dev loop is an engineering habitat, not a product surface. A capability may differ per habitat only where a decision names the difference ([DEC-052](../mvp/10-locked-mvp-decisions.md), [DEC-065](../mvp/10-locked-mvp-decisions.md)); the audit trail never differs ([BR-014](05-business-rules.md)). |
| **Code source** *(new in v1)* | Where a Run's working copy comes from: the vendor's repository (default) or a folder on the host in self-host ([BR-016](05-business-rules.md), #210). Stories always come from the vendor. |
| **Execution locus** *(new in v1)* | Where a Run executed — recorded on the Run, shown in observation ([UC-021](04-capabilities.md)): sandbox substrate, and for Local runs the working folder and branch. |
| **Run** | One execution of an Automation against one Story: lifecycle, logs, output link, cost. States: `Queued · AwaitingInput · Executing · Succeeded · Failed · Cancelled` ([BR-014](05-business-rules.md)). `Planning` and `AwaitingApproval` stay in the schema, unreachable, for the Runs already recorded in them ([DEC-067](../mvp/10-locked-mvp-decisions.md)). |
| **Hold** *(new)* | The reserved label `hitl` on a Story: while it is present no Automation starts, and a person clears it as an ordinary label change ([BR-007](05-business-rules.md), [UC-008](04-capabilities.md), [DEC-067](../mvp/10-locked-mvp-decisions.md)). The one way work waits for a person. |
| **Plan** *(retired)* | Was the proposal an Agent produced in phase 1 of an approval-gated Run. Retired with [DEC-067](../mvp/10-locked-mvp-decisions.md): every Run is single-phase, and what a person reviews is the output a step produced. Kept here because [DEC-005](../mvp/10-locked-mvp-decisions.md) locks the vocabulary — the word is not reused for anything else. |
| **Approval** *(retired)* | Was the human act of marking a Plan ready for execution. Superseded by the **Hold** ([DEC-067](../mvp/10-locked-mvp-decisions.md)). |
| **Conversation** *(new in v1)* | A live exchange between a human and an Agent. In a deployment it costs a **pass** per message ([DEC-055](../mvp/10-locked-mvp-decisions.md)); in self-host it may instead be an attached session, bounded by the machine's inactivity ([DEC-065](../mvp/10-locked-mvp-decisions.md)). |
| **Pass** *(new in v1)* | One paid agent execution answering one human message ([ADR-0008](../../adr/0008-a-live-conversation-costs-a-pass-per-message.md), [DEC-055](../mvp/10-locked-mvp-decisions.md)). |
| **Inbox** *(new in v1)* | The one cross-project list of every Run waiting on a human — an answer, a failure decision ([UC-026](04-capabilities.md)). Subtraction-based: entries leave when acted on. *(The approval category left with [DEC-067](../mvp/10-locked-mvp-decisions.md); carrying held Stories here is a named follow-up.)* |
| **Transcript** *(new in v1)* | The structured record of an agent session (#299/#300). An attached self-host session yields a raw terminal byte stream instead, rendered as `raw` lines — accepted knowingly in [DEC-065](../mvp/10-locked-mvp-decisions.md). |
| **Dispatch** | The orchestrator publishing a Run to the **Postgres outbox** that integration events already use, consumed in-process ([DEC-013](../mvp/10-locked-mvp-decisions.md) superseded). Dispatch decides *when* a Run starts; the sandbox decides *where* it executes. |
| **Mirror** | The Postgres read model of Stories (id, title, state, labels, body, last-seen), refreshed by polling and webhooks ([DEC-028](../mvp/10-locked-mvp-decisions.md), [DEC-029](../mvp/10-locked-mvp-decisions.md)). |
| **Run now** | Manually dispatching a chosen Automation against a chosen Story, bypassing detection but honoring all business rules ([BR-013](05-business-rules.md)). |
