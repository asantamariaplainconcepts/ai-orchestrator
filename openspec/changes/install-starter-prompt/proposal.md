## Why

Issue #214. The starters exist so an empty project has something to take (UC-005/UC-006 name the
Automation afterwards), but taking one is a manual chore — copy, create the file at the right
path, commit, push — and every step can silently go wrong. The spec's current "the product SHALL
NOT write any starter to any repository" was written when the alternative was spending an agent
pass; #214 is the owner's deliberate reversal for a narrower shape: **no agent pass, one bounded
git write** through the same workspace publish pipeline implement and propose already use, landing
on a **branch as a draft PR** — a human still merges, the default branch is never touched.

## What Changes

- **BREAKING (spec-level reversal, stated):** the starters requirement drops its "writes nothing"
  absolute. Offering still writes nothing; a new *Install* action writes exactly one file to a
  run-scoped branch and opens a **draft PR** with the project PAT (BR-010). No agent pass is spent.
- A starter card gains *Install*: clone → commit `<prompts directory>/<name>` → push → draft PR,
  through the existing workspace pipeline, with its stage-named refusals (clone/push/PR).
- The PR URL renders on the card — review is the human's next step, and the portal says so.
- A starter already present at its target path on the default branch refuses install naming the
  path — "an existing file SHALL always win" survives the reversal.
- No Connector → *Install* is not offered (the offer itself stays usable, as today).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `automation-configuration`: the starters requirement — "offering writes nothing" narrows to the
  offer; a new requirement adds the install-to-branch-as-draft-PR path with its refusals.

## Impact

- **Backend**: Backlog/Projects seam unchanged; a new use case drives the existing
  `ICodeWorkspace` publish pipeline (clone/commit/push/PR) with the starter's bytes — the write
  surface the product already has for Runs, reused without an agent.
- **Frontend**: the starters section (`src/frontend/features/automations/`) — Install button,
  PR link, refusal rendering.
- **Unchanged**: the starter set's content/tiers/tests, the Run path, BR-008 (the mirror is not
  involved — this is a code-repo write like implement's), BR-010.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
