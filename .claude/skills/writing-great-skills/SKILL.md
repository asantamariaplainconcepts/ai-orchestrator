---
name: writing-great-skills
description: Reference for writing and editing skills well. Use when authoring, reviewing, or refactoring a skill (including the /aio:* skills) — the vocabulary and principles that make a skill predictable.
---

<!--
Vendored and adapted for ai-orchestrator from Matt Pocock's "writing-great-skills"
(https://github.com/mattpocock/skills, skills/productivity/writing-great-skills), MIT licensed.
Original copyright retained in NOTICE. ai-orchestrator adaptations: made model-invocable (upstream was
user-invoked), and added the "ai-orchestrator marks" section below. See GLOSSARY.md for defined terms.
-->

A skill exists to wrangle determinism out of a stochastic system. **Predictability** — the agent taking the same _process_ every run, not producing the same output — is the root virtue; every lever below serves it.

**Bold terms** are defined in [`GLOSSARY.md`](GLOSSARY.md).

## Invocation

Two choices, trading different costs:

- A **model-invoked** skill keeps a **description**, so the agent can fire it autonomously _and_ other skills can reach it. It contributes to **context load** — the description sits in the window every turn. Mechanics: omit `disable-model-invocation`, and write a model-facing description with rich trigger phrasing ("Use when the user wants…, mentions…").
- A **user-invoked** skill strips the description from the agent's reach: only you, typing its name, can invoke it. Zero context load, but it spends **cognitive load** — _you_ are the index that must remember it exists. Mechanics: set `disable-model-invocation: true`; the `description` becomes a human-facing one-liner.

Pick model-invocation only when the agent must reach the skill on its own, or another skill must. When user-invoked skills multiply past what you can remember, cure the piled-up cognitive load with a **router skill**: one user-invoked skill that names the others and when to reach for each.

## Writing the description

A model-invoked **description** does two jobs — state what the skill is, and list the **branches** that should trigger it. Every word increases **context load**:

- **Front-load the skill's leading word** — the description is where it does its invocation work.
- **One trigger per branch.** Synonyms that rename a single branch are **duplication**; collapse them.
- **Cut identity that's already in the body.** Keep the description to triggers plus any "when another skill needs…" reach clause.

## Information hierarchy

A skill is built from **steps** and **reference**, ranked by how immediately the agent needs the material:

1. **In-skill step** — an ordered action in `SKILL.md`. Each step ends on a **completion criterion** — make it _checkable_ (can the agent tell done from not-done?) and, where it matters, _exhaustive_ ("every modified model accounted for", not "produce a change list"). A vague criterion invites **premature completion**.
2. **In-skill reference** — a definition, rule, or fact consulted on demand; often a flat peer-set, which is fine.
3. **External reference** — reference pushed out of `SKILL.md` into a linked file, reached by a **context pointer**, loaded only when it fires.

**Progressive disclosure** is the move down the ladder so the top stays legible. Branching is the cleanest disclosure test: inline what every **branch** needs, push behind a pointer what only some reach. **Co-location**: keep a concept's definition, rules, and caveats under one heading.

## When to split

Each cut spends a load, so split only when it earns it:

- **By invocation** — split off a model-invoked skill when a distinct **leading word** should trigger it independently. You pay context load for the always-loaded description.
- **By sequence** — split a run of steps when the steps still ahead tempt the agent to rush the one in front of it (**premature completion**).

## Pruning

- Keep each meaning in a **single source of truth** — one authoritative place; changing behaviour is a one-place edit.
- Check every line for **relevance**: does it still bear on what the skill does?
- Hunt **no-ops** sentence by sentence: if a sentence doesn't change behaviour versus the default, delete the whole sentence. Be aggressive.

## Leading words

A **leading word** is a compact concept already in the model's pretraining that the agent thinks with while running the skill. It serves predictability twice — anchors _execution_ in the body, anchors _invocation_ in the description. Refactor restatements into one word: "fast, deterministic, low-overhead" → _tight_; "a loop you believe in" → _red_. Fewer tokens, sharper hook.

## Failure modes

- **Premature completion** — ending a step before it's genuinely done. Defence: sharpen the completion criterion first; only if irreducibly fuzzy, split by sequence.
- **Duplication** — the same meaning in more than one place.
- **Sediment** — stale layers that settle because adding feels safe. The default fate without a pruning discipline.
- **Sprawl** — too long even when every line is live. Cure with the ladder: disclose reference, split by branch/sequence.
- **No-op** — a line the model already obeys by default. Fix a weak leading word with a stronger one, not more prose.

## ai-orchestrator marks (local adaptation)

On top of the above, ai-orchestrator skills MUST:

- **Do one thing.** One responsibility per skill (`grill-to-ready`, `set-issue-status`, …).
- **Never call another skill.** Composition happens in the `/aio:*` commands, not between skills.
- **Be portable.** Plain Markdown that travels with the repo; no hidden host-specific behaviour.
- **Be secure.** No secrets; confirm shared-state actions (issues, labels, PRs, GitHub edits) before executing.
- **Progressively disclose.** Push reference detail (label definitions, the Definition of Ready rubric) into linked docs, not the skill body.

The `/aio:*` toolkit is the worked example: atomic skills authored against this guide, orchestrated by commands.
