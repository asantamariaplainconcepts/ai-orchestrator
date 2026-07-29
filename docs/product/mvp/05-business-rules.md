# Business rules

Deterministic rules the product must uphold. Each becomes acceptance criteria and,
where possible, a machine gate (validator, DB constraint, command refusal).

## Runs & concurrency

- **BR-001 — One active Run per Story.** A Story with a Run in `Queued`, `Planning`,
  `AwaitingApproval` or `Executing` matches no new Automation events; new matches are
  ignored (not queued).
- **BR-002 — Project concurrency cap.** Max concurrent Runs in `Planning`/`Executing`
  per project: configurable by Admin, default **2**. Runs beyond the cap wait in `Queued`.
- **BR-004 — No automatic retries.** A `Failed` Run is terminal; humans re-trigger via
  *Run now* or by re-applying the trigger label.
- **BR-005 — Phase timeout.** Each Agent phase (Planning, Executing) has a timeout an
  Admin configures per Automation: default **30 minutes**, ceiling **60 minutes**
  ([DEC-054](10-locked-mvp-decisions.md)). The ceiling is what makes the rule keepable — a phase
  runs inside a platform execution budget, and without an upper bound there is no budget that is
  provably sufficient. **Three sites hold this contract and each names the other two**: the ceiling
  in `PhaseBudget.MaximumMinutes`, the container job's `replica_timeout_in_seconds` in
  `infra/dev/dispatch.tf` (at least the ceiling plus a drain margin), and this rule. Exceeding the
  timeout ends the Run; the reason names the limit that fired. A worker whose remaining budget is
  under one full phase does not claim more work (#144).
- **BR-006 — Human waits are untimed.** `AwaitingApproval`, `AwaitingInput` and `Queued` do
  not count toward any timeout; a Run may wait on a human — an approval, or an answer to its
  questions (#78) — indefinitely. Waiting still blocks the Story (BR-001) and is always
  cancellable (BR-012).
- **BR-012 — Cancellation.** `Queued`/`AwaitingApproval` Runs are discarded without a
  job; `Planning`/`Executing` Runs have their job terminated. Either way the Run ends
  `Cancelled` (terminal). ([DEC-041](10-locked-mvp-decisions.md))
- **BR-013 — Run now honors every rule.** Manual dispatch bypasses detection only:
  BR-001, BR-002 and the approval gate still apply.
- **BR-014 — Run auditability.** Every Run records: story reference, Automation,
  runtime, phase timestamps, plan (if any), output link (if any), log reference,
  terminal state, usage. Runs are never deleted in MVP.

## Automations & dispatch

- **BR-003 — No overlapping triggers.** Within a project, saving an Automation whose
  trigger could match a Story an existing **enabled** Automation could also match is
  rejected, naming the conflict. **Two triggers are the same trigger when the vendor would
  say so: labels and states compare case-insensitively**, and the *same* comparison is used
  when a Story is matched, so a differently-cased Automation cannot be accepted and then
  silently never fire ([DEC-056](10-locked-mvp-decisions.md)). An **exact** duplicate is
  rejected whether or not either Automation is enabled — two rows carrying one trigger are
  one trigger — while *subsumption* remains enabled-only, because a disabled Automation
  matches nothing. Enforced by a unique index over the project, the normalised label and the
  normalised state, not by a handler convention: two concurrent saves produce one row and
  one refusal (the lesson BR-001 already learned).
- **BR-007 — Approval routing.** `requiresApproval = true` → two-phase Run
  (Planning → AwaitingApproval → Executing). `requiresApproval = false` → single-phase
  (straight to Executing); no plan artifact is produced.
- **BR-015 — One event stream.** Webhook events and polling diffs normalize into
  identical story events before matching; detection behavior must not depend on the
  source. Poll interval: default 60 s, configurable per project.

## Backlog & vendors

- **BR-008 — Vendor is the source of truth for Stories.** The Mirror is a read model.
  The orchestrator writes to the vendor only through the Connector and only: trigger
  labels (UC-008), comments (UC-017/019), state transitions (UC-018), estimates
  (UC-019), PRs (UC-016).

## Security & access

- **BR-009 — Permission-based authorization.** Every operation names a required
  permission; roles are permission bundles; MVP bundles are fixed: Admin = all,
  Member = observe + trigger (labels, Run now, approve, cancel)
  ([DEC-034](10-locked-mvp-decisions.md)).
- **BR-010 — No secrets in plaintext at rest outside the habitat's secret store.**
  Connector PATs and AI provider keys exist in Postgres, logs, API responses and
  telemetry only as *names* ([DEC-014](10-locked-mvp-decisions.md),
  [DEC-030](10-locked-mvp-decisions.md)). Which store holds the value is the habitat's:
  Key Vault where one is provisioned; where none is, a location outside the application
  database, protected with the framework's data protection and a key ring held apart from
  it ([DEC-052](10-locked-mvp-decisions.md), revising this rule's original wording, which
  named Key Vault as the mechanism and so could not describe a self-hosted deployment).
  The product may store a value it is handed, under a name it derives; nothing reads one
  back.

## Cost

- **BR-011 — Usage reporting.** The runtime reports tokens + cost at run end; the
  orchestrator persists them on the Run ([DEC-038](10-locked-mvp-decisions.md)).
  A missing report yields "unknown" on the Run — never a failure.
