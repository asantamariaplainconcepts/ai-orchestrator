## Why

Runtime selection lives in two places that do not compose: every Automation names its runtime
(the field is mandatory), and every runtime's credential is one deployment-wide config key
(`Agents:{Name}:CredentialSecretName`). Changing a project's runtime means editing Automations one
by one, and two projects cannot bill to two different keys. Issue
[#244](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/244): both move to the
Project — a default runtime, an optional credential **name** per runtime (BR-010), and a human
choice at every launch point, recorded on the Run (BR-014).

## What Changes

- A Project carries runtime settings: a **default runtime** and, per runtime, an optional
  **credential secret name**. Admin-writable from Settings; names stored, values never (BR-010);
  the read is Admin-scoped (BR-009).
- An Automation's runtime becomes **optional**: unset means "the Project default, resolved at
  execution time" — changing the default changes future Runs. Existing Automations keep their
  explicit runtime; the migration changes no behaviour.
- The execution chain resolves in order: the human's per-Run choice, the Automation's explicit
  runtime, the Project default, the deployment default. The credential resolves project name →
  deployment name → none (free model, DEC-044).
- Every human launch point — Run now, a re-run, launching on a change — pre-selects the resolved
  runtime and lets the human change it **for that Run only**; the choice is recorded on the Run
  (the column run-on-a-pr already added). Label-triggered Runs involve no human and no override.
- **A local-lane defect is fixed under AC6:** the runtimes export `GITHUB_TOKEN` / API-key env
  vars unconditionally, so a Local Run's empty vendor token *shadows the host CLI's own auth*.
  Empty means "do not set", so the host's identity survives.

Not breaking: the Automation update endpoint keeps accepting an explicit runtime, and absent means
default — the same absent-versus-set discipline `steps`/`tiers` already use.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `automation-configuration`: the Project gains runtime settings (one added requirement), and the
  Automation's runtime requirement is modified — optional, with "Project default" as the stated
  meaning of absent.
- `agent-execution`: the runtime-selection requirement is modified — the chain replaces "the
  Automation's value", and an empty credential never shadows a host identity.
- `run-orchestration`: one added requirement — the human's per-Run runtime choice at launch
  points, pre-selected from the resolution and recorded on the Run.

## Impact

- Projects module: `Project` runtime settings (+ migration: default runtime column, credentials
  table), `Automation.Runtime` nullable (+ same migration), settings endpoints, Contracts read for
  Runs.
- Runs module: executor resolution chain; Run now / re-run / run-on-change accept the optional
  choice (Run.RuntimeName records it — exists since run-on-a-pr).
- ServiceDefaults: runtimes stop exporting empty env vars.
- Frontend: Settings section; the runtime selects at the launch dialogs; the Automation form's
  "Project default" option.
- Tests: functional coverage per AC; the AC6 shadowing fix pinned where it is reachable.

No `OPN-*` open. Change id: `project-runtimes`.
