# Proposal: repo-defined-actions

## Why

Issue #150 (ACT-001 configures, ACT-003 executes, ACT-002 reads; UC-005, UC-006, UC-017). The action
catalogue is a closed enum with its prompts hardcoded in the executor, so a team wanting an Agent to
do anything the catalogue does not name is stuck.

The product already decided this principle in miniature. DEC-048 made the grill's rubric the
project's own document, read live, *because a product-wide readiness bar would impose one team's
standards on every repository*. The same argument applies to the prompt itself: a fixed catalogue is a
fixed set of things one team thought of.

## What changes

- **An Automation may name a markdown file as its action** (design D1), read live from the project's
  repository at execution time — the rubric's precedent, not a new mechanism.
- **The body is the prompt; frontmatter is ignored** (design D2). Any leading YAML block is the
  *file's* wiring for other runners, and the Automation is already this product's wiring — which makes
  an existing agentic workflow file reusable unchanged.
- **The write surface is one comment** (design D3): the agent's answer is posted on the Story, exactly
  RefineOrComment's bounded surface. A repository prompt cannot acquire new powers by asking for them.
- **Refusals name the path** (design D4): a missing file, or a body empty after stripping frontmatter,
  fails before any agent runs. No fallback prompt and no silent catalogue substitute.
- **Recorded as the next entry in DEC-048's lane** (design D5).

## Impact

- Specs: `agent-execution` — one ADDED requirement.
- Docs: DEC-057 recorded.
- Code: one enum value, one branch in the executor's simple-action path, a frontmatter strip, and the
  action offered in the portal with its path field.
- No schema change: the path reuses `RubricPath`, which is already "the document this action reads"
  and already flows through the API and the canvas — the same reuse `sync-action` made for its
  close-out procedure.

## Out of scope

- The **PR shell** — a repository prompt that prepares a workspace and publishes. Its own slice, and
  deliberately after this one: the comment shell is the surface where a wrong prompt is cheap.
- Adding this action to the seeded defaults. It cannot have a default, because it is nothing without
  a path.
- Interpreting frontmatter in any way, including honouring a model or tool list it declares.
