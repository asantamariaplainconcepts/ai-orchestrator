## Context

Three pieces already exist and this change joins them: the seam can list a repository directory
(#215), the starter installer can write files and open a draft pull request (#214), and
`SetUpDefaultAutomations` can create a wired pipeline idempotently (#212). What is missing is the
question asked first — *what does this repository already have?* — and today's answer is assumed
rather than asked.

`ds-connect` is the evidence: `.claude/commands/ds/` holds six files whose names are the pipeline's
own steps. Nothing about the current flow can see them, because the prompts directory defaults to
`ai/prompts` and nothing looks anywhere else.

## Goals / Non-Goals

**Goals:**
- A repository with its own pipeline gets it wired, not duplicated.
- Nothing is written before a human has seen what was found.
- A step with no file is the only thing a starter fills.

**Non-Goals:**
- Generating a prompt for a step the repository lacks and the catalogue does not cover.
- Editing or judging prompt content — the file is the team's, whatever it says.
- Recursive discovery beyond one subdirectory level.
- A teardown action (named out of scope on the issue).

## Decisions

**D1 — discovery proposes, it never picks.** The candidates are searched, and the result is shown
with what each holds; saving the prompts directory happens on confirmation. *Alternative
rejected:* choosing the directory with the most matches automatically — it would silently
reconfigure a project the first time somebody pressed a button, and the one thing worse than not
finding a pipeline is adopting the wrong one.

**D2 — the candidate list is short, conventional and ordered.** The Connector's configured
directory first (an explicit answer beats a guess), then `ai/prompts`, then `.claude/commands` and
its immediate subdirectories. One level, because `ds-connect`'s `ds/` is one level and unbounded
recursion turns a form action into a repository crawl. *Alternative rejected:* a configurable
search list — configuration to find the configuration.

**D3 — the name is the mapping, and an unmatched file is reported rather than interpreted.** A
file called `grill.md` is the grill step whatever directory it is in; `sprint-notes.md` is not a
pipeline step and gets no Automation. Inventing a trigger from an unrecognised filename would
create a label nobody applies and an Automation that never fires — the "configurable thing that
silently never executes" the automation spec already forbids.

**D4 — the gaps are one pull request, not one each.** #214 opens a PR per starter, which is right
when a human picks one. Filling four gaps that way is four reviews of the same decision, so this
path composes one branch carrying every missing starter. Same pipeline, same refusals, one review.

**D5 — the report is the product of the action.** Created, skipped-and-why, found-but-not-wired,
installed-with-its-PR. A press whose outcome is "done" teaches nobody what happened to a
repository they did not read first.

## Risks / Trade-offs

- [Several candidate directories with files] → all are offered; the human picks. The awkward case
  is honest rather than resolved by a heuristic that will be wrong for somebody.
- [A repository whose prompts are named differently] → they are reported found-but-not-wired, and
  the Admin wires them by hand with the picker (#215). The button does less rather than guessing.
- [One PR for several starters is harder to reject partially] → true, and the alternative was four
  PRs for one decision. A partial rejection is an edit to the branch, which is what review is.
- [Discovery costs several vendor reads] → they are listings, names only, on an explicit press.

## Migration Plan

Additive: the existing defaults action keeps working for a project with nothing, because that is
the case where adoption finds nothing and every step is a gap. No schema, no configuration change.

## Open Questions

(none)
