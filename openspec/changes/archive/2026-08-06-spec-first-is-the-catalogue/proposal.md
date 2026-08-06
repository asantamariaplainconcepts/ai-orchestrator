# Proposal: spec-first-is-the-catalogue

## Why

[#269](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/269). The setup card offers
a repository two things, and the wrong one is installable. The portable tier — triage, explain,
implement, tests, review — assumes no methodology and installs on one press. The spec-first workflow,
the loop this product's own development runs on, is recognised only where a repository already had it
and is installed never (`automation-configuration`: *"a step from an opt-in tier is adopted but never
installed"*). Reaching it means six presses of the per-starter route (#214), six branches, six pull
requests — and a loop still unwired at the step that writes the code, because `aio-implement.md`
carries no `automation` block by construction: the portable `implement.md` owns `ai:implement`.

Worse, the tier's prerequisites are printed and then abandoned. An Admin who assembles the workflow
by hand ends with six prompts that read documents their repository does not contain, and learns it
from a failed Run rather than from the card — the exact failure `automation-configuration` introduced
tiering to prevent (*"a prerequisite is visible before it is needed"*).

UC-005 in bulk should install the workflow worth adopting, together with what that workflow needs to
run, in one reviewable pull request.

## What Changes

- **BREAKING — the portable tier is removed; the spec-first workflow is the catalogue.** Its five
  prompts leave the manifest and the repository. `aio-implement.md` gains the `ai:implement` wiring
  the collision denied it, so the loop closes at the step that writes code, and the catalogue's
  duplicate-trigger refusal keeps its no-exception form instead of growing a carve-out.
- **BREAKING — adoption stops recognising the removed steps.** A repository holding `triage.md`,
  `explain.md`, `tests.md` or `review.md` has those files reported as *found, not wired*, where today
  each becomes an Automation. Named here because it is a regression a reader must not discover.
- **BREAKING — with no consent, the setup action installs nothing.** No ungated tier remains, so an
  unconsented press wires only what the repository already holds: no branch, no pull request. #262's
  bodyless call keeps its wire shape and changes its outcome; that is the breaking part.
- **A tier is consented to by name, and the consent says what will be written.** The card presents
  the tier with a switch, off by default, listing what a press with it on writes — the prompts *and*
  the prerequisite files. The consent is per-invocation and never persisted, so convergence stays
  unconditional as `default-automations` already requires.
- **A tier's prerequisites are catalogue content, and travel in the same pull request.** The manifest
  gains a `prerequisites` block per tier: paths outside the prompt directory, with the bytes to write
  there. The product hardcodes no methodology here either — a fork edits the manifest and this
  behaviour follows it, the same discipline #190 and #212 established for wiring.
- **An existing file always wins, unchanged.** A repository that already holds
  `docs/process/definition-of-ready.md` or an `openspec/` layout receives neither. The seed lands only
  where there is nothing.
- **The report separates prompts from prerequisites.** An Admin who consented to prompts must see,
  without opening the diff, that files outside the prompt directory were written.

## The decision this records

An ADR (`docs/adr/0012-*`) and a `DEC-*` entry, revising **DEC-048's rubric clause**: *"the rubric is
always the project's own document, read live, because a product-wide readiness bar would impose one
team's standards on every repository it touches."* `automation-configuration` restates the same
reasoning at the adoption requirement — *"the copy is the weaker of the two."*

The revision is narrower than a reversal, and the ADR must say why: **"the weaker of the two" presumes
two.** Where a team has its own document, it still wins — that rule is not being touched, and is
asserted by two of this change's scenarios. Where a team has none, the comparison has no second term,
and the alternative shipped is not a team's own rubric but a workflow whose first Run fails. DEC-048's
read-time invariant — the `GrillToReady` action reads the project's document live, never a bundled
one — survives untouched; what changes is that the product may *seed* the document it will later read.

The ADR must also record the cost plainly: on day one, every repository that presses the button holds
the same readiness bar. The mitigation is that it is theirs from the moment it lands.

## Honest note on scope

"Everything the prompts read" cannot be taken literally. `docs/product/mvp/` holds *this* product's
identity — `ACT-001..004`, `UC-*`, `BR-*`, `DEC-*`, the bounded contexts — and copying it into
somebody else's repository would ship AI Orchestrator's scope as their template. What the catalogue
carries with real content is what transfers: the readiness rubric, and the `RULE-001..007` shaping
rules it must cite (`definition-of-ready` forbids the rubric from restating them, so the two travel
together or the seed is broken on arrival). `openspec/config.yaml` ships as structure with its
project-context section left an explicit TODO, because context is the one part that cannot be
inherited. The remaining documents the grill reads ship as skeletons — headings and an ID convention —
so the rubric's links resolve instead of dangling. This is recorded as an assumption on #269.

## Capabilities

### New Capabilities

- `workflow-prerequisites`: what a starter tier declares it needs beyond prompts, and how consenting
  to that tier writes those files — into the same branch and pull request as the prompts, never over
  a file that already exists, and reported apart from the prompts.

### Modified Capabilities

- `automation-configuration`: the tier requirement stops describing a portable tier and a workflow
  tier as a pair; *"adopted but never installed"* becomes *never installed without explicit consent*;
  the setup card's plan gains the per-tier consent switch that names what a press will write; the
  starter set served and installable shrinks to one tier.
- `default-automations`: the action no longer describes "each wired portable-tier prompt"; it accepts
  the tier consent, installs nothing without it, and reports installed prerequisites as a fact
  distinct from installed prompts.

## Impact

**API** — `POST /api/projects/{id}/automations/set-up-defaults` gains an optional tier-consent field
on its request, and its response's installed report gains the prerequisite files. `GET
.../automations/discover-pipeline` gains the tiers themselves: id, title, the `requires` text, and the
prerequisite paths a consent would write, so the switch can state its consequence without a
round-trip. `GET .../starter-prompts` serves one tier where it served two — **BREAKING** for any
caller enumerating the portable starters, and `POST .../starter-prompts/install` answers
`Starter.Unknown` for their `saveAs` values.

**Code** — `Starter/manifest.json` (the portable tier removed, `aio-implement.md` wired, a
`prerequisites` block added), `Starter/portable/*.md` (deleted), `Starter/workflow/prerequisites/*`
(new content), `StarterCatalogue` (the tier record gains prerequisites), `PipelineSteps`
(`Installable` becomes a function of consent rather than of `Requires is null`), `DiscoverPipeline`
(the response gains tiers), `SetUpDefaultAutomations` (`Request`/`Command`/`Response`, the consent
filter, and `FillGaps` writing two kinds of file into one branch), `StarterInstaller` (unchanged seam,
new caller shape), `WorkflowSetupSection.tsx` + `useWorkflowSetup.ts` + `en.ts` (the switch, its
consequence list, the split report), `shared/http/mock.ts`.

**Tests** — the manifest-enumeration and starter-body tests lose five prompts and gain the
prerequisite files (a prerequisite that fails to load is worse than none, for the same reason a
starter is); `PipelineAdoption_Should_Constraint` and `SetupPlan_Should_Constraint` are rewritten
around one tier and the consent; `StarterCatalogue_Should_Constraint` covers the new manifest block.

**Docs** — `docs/adr/0012-*` (new), `docs/product/mvp/10-locked-mvp-decisions.md` (the `DEC-*`
revising DEC-048), `docs/product/manual/README.md` (the card's behaviour).

**Not affected** — no migration, no new column, no new permission (BR-009's `ManageAutomations` gate
covers the consent), no change to the queue message schema, the Runtime seam, Aspire wiring, or CI.
BR-003's normalised-trigger identity is upheld and gets simpler: with one tier, nothing contends for
`ai:implement`. No `OPN-*` decision is open (RULE-006 satisfied — all closed).
