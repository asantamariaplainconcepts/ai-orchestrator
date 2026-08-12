## Why

Work waits for a person in two unrelated ways today: `requiresApproval` pauses a Run mid-flight on
a Plan nobody can see from the vendor (BR-007, DEC-039, DEC-040), and an unclaimed lifecycle
boundary means "somebody carries this across" with nothing recorded anywhere (BR-006, #310). A
person looking at the GitHub issue — where the work actually lives — cannot tell that anything is
waiting, or why.

[DEC-062](../../../docs/product/mvp/10-locked-mvp-decisions.md) already accepted, as a stated cost,
that "BR-007's approval gate is a **workflow control** now, not a containment control": once the
catalogue became one action running the repository's own prompt, "a plan phase publishes nothing"
became a prompt-level promise rather than a product guarantee. A workflow control does not need a
second Run phase, two extra Run states and a review surface — it needs to be visible where the work
is. Issue [#321](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/321).

## What Changes

- **BREAKING** — `requiresApproval` is removed from the Automation aggregate, from
  `AutomationTrigger` and `AutomationDetail` in `AiOrchestrator.Modules.Projects.Contracts`, and
  from the Automations form. Contracts are consumed only in-process by the Runs module (no
  outbox-message or HTTP contract carries the flag), so no integration contract — Aspire wiring,
  host csproj, outbox message schema, CI — changes.
- **BREAKING** — a new decision supersedes **DEC-039** ("approval is a per-Automation toggle") and
  **DEC-040** ("approval shape: plan-then-approve"), and **BR-007** is rewritten from approval
  routing to the hold rule. Both were locked; this is the reversal DEC-062's stated cost invites,
  argued rather than assumed (RULE-006).
- A reserved label — `hitl` — becomes the single human gate. While a Story carries it, **no Run is
  created**: not by event matching (UC-011), not by *Run now* (UC-012, BR-013).
- An Automation that stops for a person applies `hitl` on success through its existing output
  labels — DEC-062's carve-out already licenses that write, so **no new field and no migration**.
- Removing the label is an ordinary vendor label change (UC-008): the resulting story event matches
  normally and the next Automation's Run is created. The hold needs no resume machinery.
- A hold gates **creation, not execution** — a Run already `Executing` finishes and applies its
  result.
- `Planning`, `AwaitingApproval`, `DecideOnPlan` and the `Plan`/`ApprovedAt` columns are left in
  place but **unreachable**, deleted in a named follow-up. This is DEC-062's own precedent for the
  dormant `AwaitingInput` wait: Run states were out of scope there, and are out of scope here.
- UC-013 (Member reviews a Plan) and UC-015 (Agent produces a Plan) retire; UC-005, UC-006 and
  UC-011 lose their approval clause; UC-026's approval category becomes unreachable, with its
  replacement named as a follow-up rather than built here.

## Capabilities

### New Capabilities

- `story-hold`: a reserved label on a Story that stops any Automation from starting, who may apply
  and clear it, what it does to Runs already in flight, and how an Automation applies it on success.

### Modified Capabilities

- `run-orchestration`: matching no longer branches on `requiresApproval`; a held Story refuses
  creation on both the event path and *Run now*; the requirement "an approval-gated Run pauses on
  its Plan and a human decides" is removed, along with the two-phase refusal scenarios that
  preceded it.
- `automation-configuration`: the approval control and its "says what it does" requirement leave
  the create and edit forms; an Automation is configured to stop by marking the hold.
- `default-automations`: the starter catalogue's `automation` blocks stop carrying
  `requiresApproval`; the spec-first tier's propose, implement and sync steps apply the hold among
  their output labels instead, so the shipped chain still stops three times — after each step acts,
  on the Story, rather than inside the Run before it acts.

## Impact

- **Backend** — `Automation` aggregate + its EF configuration and a migration dropping the column;
  `IAutomationCatalog` (`AutomationTrigger`, `AutomationDetail`); `RunCreator.Create` gains the hold
  refusal as a new non-created outcome beside `AlreadyActive`, so every creation path honours it;
  `StoryChangedHandler` loses nothing (the refusal lives one level down, where *Run now* also
  passes). `IStoryReader`'s snapshot already carries labels — no read surface widens.
- **Frontend** — the approval field and its `GateChip` leave the Automations form and the board
  preview; `summarise()`'s human-stop count is computed from holds and unclaimed boundaries; i18n
  entries for the approval control are removed and hold copy added.
- **Corpus** — a new numbered DEC (allocated against `origin/main`, per `decision-records`);
  BR-007 rewritten; BR-001, BR-006, BR-013 lose their `AwaitingApproval` clauses; UC-005, UC-006,
  UC-011, UC-013, UC-015, UC-026 and ACT-001/ACT-002 updated; the glossary gains the hold.
  `openspec/config.yaml`'s project context still states "Approval-gated runs are two-phase" and
  must follow.
- **Tests** — approval-path functional tests retire; new coverage for the four hold behaviours
  (event refusal, *Run now* refusal, executing Run unaffected, clearing resumes the flow).
- **Not touched** — the Inbox's own surface, the plan-phase machinery itself, the board preview's
  read-only stance (its own issue), and the repository's `/aio:*` command gates (its own issue).
