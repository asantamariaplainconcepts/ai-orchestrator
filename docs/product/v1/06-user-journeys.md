# User journeys

End-to-end narratives across capabilities. IDs cite [capabilities](04-capabilities.md) and
[business rules](05-business-rules.md).

## J1 — The golden path (the proven claim, DEC-002)

An **Admin** signs in (UC-001), creates *Project Phoenix* (UC-003), configures its GitHub
Connector — save verifies the credential live (UC-004) — and creates an Automation: label
`ai-implement` → action *Implement→PR*, runtime Claude Code headless,
`requiresApproval = true`, 30-min timeout (UC-005).

A **Member** opens the backlog (UC-007) and applies `ai-implement` to story #42 from the
website; the label lands on GitHub (UC-008). Detection picks it up (UC-009/010), matching
creates a Run (UC-011; BR-001/002 hold), and dispatch publishes the plan phase. An **Agent**
starts in a sandbox of its own: it reads #42 and writes its Plan onto the Run (UC-015); the
Run pauses at `AwaitingApproval` (BR-006).

The Member reviews the Plan (UC-013) and approves. Execution dispatches; a second Agent phase
clones the repo, implements the story, opens a PR, and links it on the Run and the Story
(UC-016). The Run ends `Succeeded` with logs and cost attached (UC-020, UC-021). The Member
reviews the file changes beside the approved Plan (UC-028) and clicks through to the PR.

## J2 — Frictionless refinement (no approval)

A second Automation: label `ai-refine` → *Refine/comment*, opencode runtime,
`requiresApproval = false`. A Member labels a vague story; the Run goes straight to
`Executing` (BR-007), the Agent posts refinement questions as a story comment (UC-017), the
Run succeeds. Total human involvement: one label.

## J3 — Failure, observation, re-run

An Executing Run hits its timeout (BR-005) and is marked `Failed`. The Member opens the Run,
reads the logs (UC-021, UC-027), sees the story was ambiguous, edits the story in the vendor,
then hits *Run now* (UC-012). BR-001 allows it — the failed Run is terminal. The new Run
succeeds. No automatic retry ever fired (BR-004).

## J4 — Governance says no

An Agent's Plan proposes touching code outside the story's scope. The reviewer rejects it
(UC-013) — the Run ends `Cancelled`, nothing executed. Separately, an Admin watches a runaway
Executing Run and cancels it (UC-014, BR-012); the job is terminated.

## J5 — The self-hosting developer

A **developer** runs the product on their own machine with Docker and git — no Azure, no SDK
(DEC-049). Their Connector points at a GitHub backlog, but its code source is a folder on the
host (UC-004, BR-016). They label a story; the Run executes in an `sbx` sandbox and its output
is a local branch — committed, never pushed (UC-016).

Mid-run, the agent asks a question and the Run pauses at `AwaitingInput` (BR-006). The Inbox
surfaces it (UC-026). Because this machine is theirs, they do what a deployment forbids: open
the machine's sandbox surface (UC-029), attach to the Run's sandbox, and answer in the
agent's own session (DEC-065). The attach is recorded — who, when, which sandbox (BR-014).
The Run finishes; its branch is waiting in their checkout.

The same person is Admin and Member throughout, and no rule noticed (01-actors).
