# Business rules

Deterministic rules the product must uphold. Each becomes acceptance criteria and, where
possible, a machine gate (validator, DB constraint, command refusal). Carried from the old
corpus unchanged in force; only citations moved.

## Runs & concurrency

- **BR-001 — One active Run per Story.** A Story with a Run in `Queued`, `Planning`,
  `AwaitingApproval`, `AwaitingInput` or `Executing` matches no new Automation events; new
  matches are ignored (not queued). *(UC-032 will need this rule to speak about sibling Runs
  before it is proposable — that is a decision, not a drive-by edit.)*
- **BR-002 — Project concurrency cap.** Max concurrent Runs in `Planning`/`Executing` per
  project: configurable by Admin, default **2**. Runs beyond the cap wait in `Queued`.
- **BR-004 — No automatic retries.** A `Failed` Run is terminal; humans re-trigger via
  *Run now* or by re-applying the trigger label.
- **BR-005 — Phase timeout.** Each Agent phase (Planning, Executing) has a timeout an Admin
  configures per Automation: default **30 minutes**, ceiling **60 minutes**
  ([DEC-054](../mvp/10-locked-mvp-decisions.md)). Three sites hold this contract and each
  names the other two: `PhaseBudget.MaximumMinutes`, the job timeout in `infra/dev/dispatch.tf`,
  and this rule. Exceeding the timeout ends the Run; the reason names the limit that fired.
  A worker whose remaining budget is under one full phase does not claim more work (#144).
  *(DEC-065 left stated: a timeout that bounds unattended work cannot also bound work a human
  is typing into — the into-the-agent session needs a rule here before it is implementable.)*
- **BR-006 — Human waits are untimed.** `AwaitingApproval`, `AwaitingInput` and `Queued` do
  not count toward any timeout; a Run may wait on a human indefinitely. Waiting still blocks
  the Story (BR-001) and is always cancellable (BR-012).
- **BR-012 — Cancellation.** `Queued`/`AwaitingApproval` Runs are discarded without a job;
  `Planning`/`Executing` Runs have their job terminated. Either way the Run ends `Cancelled`
  (terminal). ([DEC-041](../mvp/10-locked-mvp-decisions.md))
- **BR-013 — Run now honors every rule.** Manual dispatch bypasses detection only: BR-001,
  BR-002 and the approval gate still apply.
- **BR-014 — Run auditability.** Every Run records: story reference, Automation, runtime,
  phase timestamps, plan (if any), output link (if any), log/transcript reference, execution
  locus, terminal state, usage. Runs are never deleted. Affordances may differ per habitat;
  what a Run records may not.

## Automations & dispatch

- **BR-003 — No overlapping triggers.** Within a project, saving an Automation whose trigger
  could match a Story an existing **enabled** Automation could also match is rejected, naming
  the conflict. Two triggers are the same trigger when the vendor would say so: labels and
  states compare case-insensitively, and the same comparison is used at match time
  ([DEC-056](../mvp/10-locked-mvp-decisions.md)). An **exact** duplicate is rejected whether or
  not either Automation is enabled; *subsumption* remains enabled-only. Enforced by a unique
  index, not a handler convention: two concurrent saves produce one row and one refusal.
- **BR-007 — Approval routing.** `requiresApproval = true` → two-phase Run
  (Planning → AwaitingApproval → Executing). `requiresApproval = false` → single-phase; no
  plan artifact is produced.
- **BR-015 — One event stream.** Webhook events and polling diffs normalize into identical
  story events before matching; detection behavior must not depend on the source. Poll
  interval: default 60 s, configurable per project.
- **BR-016 — A Local run requires a clean working tree.** A Run whose execution locus is the
  host's own folder (#210, self-host — [DEC-049](../mvp/10-locked-mvp-decisions.md)) is refused
  **before any write** when the folder has uncommitted changes; the refusal names the folder.
  The check runs again at execution, because the folder belongs to a person who may have typed
  in between — that failure ends the Run with the same sentence and restores their checkout.
  The run branch (`ai/{story}-{slug}`) is the Run's output: committed, never pushed, no PR.

## Backlog & vendors

- **BR-008 — Vendor is the source of truth for Stories.** The Mirror is a read model. The
  orchestrator writes to the vendor only through the Connector and only: trigger labels
  (UC-008), comments (UC-017/UC-019/UC-028), state transitions (UC-018), estimates (UC-019),
  PRs (UC-016/UC-025).

## Security & access

- **BR-009 — Permission-based authorization.** Every operation names a required permission;
  roles are permission bundles; the bundles are fixed: Admin = all, Member = observe + trigger
  (labels, Run now, approve, answer, cancel) ([DEC-034](../mvp/10-locked-mvp-decisions.md)).
- **BR-010 — No secrets in plaintext at rest outside the habitat's secret store.** Connector
  PATs and AI provider keys exist in Postgres, logs, API responses and telemetry only as
  *names*. Which store holds the value is the habitat's: Key Vault where one is provisioned;
  where none is, a location outside the application database, protected with the framework's
  data protection and a key ring held apart from it
  ([DEC-052](../mvp/10-locked-mvp-decisions.md)). The product may store a value it is handed,
  under a name it derives; nothing reads one back.

## Cost

- **BR-011 — Usage reporting.** The runtime reports tokens + cost at run end; the orchestrator
  persists them on the Run ([DEC-038](../mvp/10-locked-mvp-decisions.md)). A missing report
  yields "unknown" on the Run — never a failure.
