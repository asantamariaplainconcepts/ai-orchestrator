# Proposal: run-now

## Why

Issue #21 (UC-012). The loop runs on labels; UC-012 adds the human bypass — and, once failure
exists, the BR-004 re-run path. BR-013's constraint shapes the whole design: Run now bypasses
**detection only**, so it must share #17's creation path — a second path would fork BR-001,
BR-002 and the approval gate into two implementations that drift.

## What Changes

- **Extract the shared creation path**: the rule-enforcing core of `StoryChangedHandler`
  (story read, BR-007 lane split, BR-001 pre-check + index catch, BR-002 cap, create, dispatch)
  becomes `RunCreator`, used by both the event handler and the new endpoint. Matching keeps its
  silent-ignore semantics; Run now maps the same outcomes to human-facing responses.
- **`POST /api/projects/{projectId}/runs`** with `{vendorStoryId, automationId}`: validates the
  Story (mirror) and the Automation (enabled, this project), skips only the trigger-label test,
  and returns the Run — or the rule that refused it (409 for BR-001, the stated two-phase
  limitation for BR-007, distinct validation errors otherwise). At the BR-002 cap the Run is
  created `Queued` and the response says so.
- **Portal**: Run now on each backlog row (Automation picker when several are enabled), refusals
  surfaced, Runs section reflects the result.

## Impact

- Affected specs: `run-orchestration` (adds the manual-dispatch requirement).
- Touched: Runs module (RunCreator extraction + one endpoint), frontend (backlog rows +
  catalog), Runs functional tests.
- Out of scope: two-phase lane, Failed-specific re-run mechanics, any bypass beyond detection.
