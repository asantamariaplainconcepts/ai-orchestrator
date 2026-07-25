# Glossary

Defined terms used in [`SKILL.md`](SKILL.md). Vendored/adapted from Matt Pocock's writing-great-skills (MIT — see [`NOTICE`](NOTICE)).

- **Predictability** — the agent taking the same _process_ every run (not producing the same output). The root virtue a skill exists to serve.
- **Context load** — the cost a model-invoked skill's description pays by sitting in the window every turn.
- **Cognitive load** — the cost a user-invoked skill pays by requiring _you_ to remember it exists.
- **Model-invoked** — a skill the agent can fire autonomously (has a trigger description); other skills can reach it.
- **User-invoked** — a skill only you can fire by name (`disable-model-invocation: true`); zero context load.
- **Router skill** — one user-invoked skill that indexes others and says when to reach for each.
- **Description** — the model-facing frontmatter that states what the skill is and lists trigger branches.
- **Branch** — a distinct way the skill is used; different runs taking different paths.
- **Step** — an ordered action in `SKILL.md`, ending on a completion criterion.
- **Completion criterion** — the condition that tells the agent a step is done; should be _checkable_ and, where it matters, _exhaustive_.
- **Reference** — a definition, rule, or fact consulted on demand (in-skill or external).
- **Context pointer** — the wording that sends the agent to an external file; its phrasing decides when/how reliably it fires.
- **Information hierarchy** — the ladder (in-skill step → in-skill reference → external reference) ranked by immediacy.
- **Progressive disclosure** — moving material down the ladder into linked files so the top stays legible.
- **Co-location** — keeping a concept's definition, rules, and caveats under one heading.
- **Granularity** — how finely skills are divided; each cut spends a load.
- **Leading word** — a compact pretrained concept the agent thinks with; anchors execution and invocation in few tokens.
- **Legwork** — the digging the agent does within the work, driven by a demanding completion criterion.
- **Single source of truth** — one authoritative place for each meaning.
- **Relevance** — whether a line still bears on what the skill does.
- **No-op** — a line the model already obeys by default; pays load to say nothing.
- **Premature completion** — ending a step before it's genuinely done.
- **Duplication** — the same meaning in more than one place.
- **Sediment** — stale layers that accumulate without a pruning discipline.
- **Sprawl** — a skill too long even when every line is live.
