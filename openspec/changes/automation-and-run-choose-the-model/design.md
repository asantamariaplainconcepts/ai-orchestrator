## Context

The runtime resolution chain already exists and is well worn: `run.RuntimeName ??
automation.Runtime ?? project.DefaultRuntime ?? deployment`. This change adds a second axis with
the same shape, so most of the design is "mirror what is there". What is genuinely new is **where
the offered choices come from**, and that is where the whole design lives.

Two facts, both measured on the authoring machine before any of this was written.

**Claude Code cannot enumerate its models.** `claude --help` documents `--model <model>` and no
listing command exists. Worse, the plausible aliases are not safe to hardcode: against Claude Code
2.0.44, `sonnet` answers, `opus` resolves to `claude-opus-4-1-20250805` and returns 404
not_found, and `fable` is not an alias at all and is passed through literally to another 404. A
hardcoded list of three would ship two broken options today.

**opencode can enumerate, and the answer depends on where you ask.** `opencode models` exits 0 with
one model per line. On the host it returned **41** models. Inside a sandbox created from the
opencode template, with the machine owner's session carried in (#288), it returned **495** — and
the `github-copilot/*` entries are there because the carried session unlocks that seat.

That 41-versus-495 is the finding that decides everything below. A list gathered in the wrong place
is not slightly stale; it is wrong by an order of magnitude.

## Goals / Non-Goals

**Goals.** A model chosen per Automation and overridable per Run. An offer that reflects what the
executing machine can actually run. A rejected model that fails legibly. The model recorded beside
the cost it caused.

**Non-Goals.** Teaching a runtime to enumerate what it has no command for. Per-project defaults.
Provider selection as a separate axis. Automatic model routing. Spend caps. Validating a written
model before a Run uses it.

## Decisions

### D1 — Two discovery mechanisms, because there are two CLIs

The tempting design is one uniform mechanism. Both candidates fail.

*Uniform configuration* would mean an operator maintaining a list of 495 opencode models by hand,
re-copied whenever a provider ships anything, when the CLI will recite it on request.

*Uniform probing* is impossible: Claude has no command to probe. Faking one — running `claude
--model X` to see whether it errors — costs a real model call per candidate and cannot enumerate,
only test.

So the mechanism is a property of the runtime, declared where runtimes are already composed: a
runtime either answers "here is how you list my models" or it does not, and the product's behaviour
follows from that answer rather than from a branch on the runtime's name.

*Alternative rejected — configuration for both, with opencode's list seeded from a script.* It
turns a live fact into a build artifact, and DEC-044 already learned that opencode's surface moves.

### D2 — The enumeration is asked of the machine that will run, never of this process

This is #279's design D6 again, and the 41-versus-495 measurement is why it is not a formality. The
question "which models can be used" is answered by the CLI, its image, and the session it holds —
all three of which live on the executing machine. In a sandboxed habitat that is inside a sandbox;
in the local habitat it is this process.

So the enumeration goes through `IAgentProcessHost`, beside `CliAnswers`, which already exists to
ask exactly this class of question in exactly the right place.

### D3 — The list is cached, but not on `CliAnswers`' reasoning

`CliAnswers` caches on its own long cadence, and its recorded justification is that the answer is a
property of the **template image**, which does not move between two probes.

That justification does not transfer. A model list is a property of the image **and of the session
the sandbox holds** — the `github-copilot/*` entries exist because #288 carried a seat in. A cache
keyed on the command alone would serve one developer's models to a habitat that carries a different
session, which is a correctness bug wearing a performance costume.

So the cache is keyed on what the answer actually depends on, and a habitat that carries sessions
invalidates it when the carried set changes. Creating a sandbox costs seconds, so caching is
necessary; it just has to be honest about what it is caching.

### D4 — Resolution mirrors the runtime's, one level at a time

`run.Model ?? automation.Model ?? deployment`. Deliberately **one level shorter** than the
runtime's: #291 puts a per-project model default out of scope, so there is no project level to
consult. Resolution happens at execution time, so changing the deployment default changes future
Runs without touching an Automation — the same property the runtime chain already promises.

The model resolves **independently of the runtime**, which raises the obvious hazard: an Automation
whose model belongs to a different runtime. The runtime rejects it and D5 says how that reads.

### D5 — A rejected model fails naming itself, and nothing retries

Nothing retries (BR-004), so the failure is the whole message. The measurement helps here: both
CLIs reject an unknown model cleanly and **name the model in the error** — an invalid model and a
valid-but-unavailable one fail identically, which is the honest outcome, because the product cannot
tell those apart and should not pretend to.

So the Run's failure reason names the model asked for and the runtime that refused it, in the same
place the credential remedies already live (#279 design D3), rather than surfacing a raw 404.

*Alternative rejected — validate the model at save time.* For opencode it would duplicate the
enumeration at the wrong moment; for Claude it would require a real model call per save; and for
both it would make saving fail on a machine that happens to be down, which is a bad reason to be
unable to edit an Automation.

### D6 — An unreachable machine offers nothing and says so

An empty list and "I could not ask" are different sentences, and rendering the second as the first
would tell a developer their runtime has no models. This is the same lesson the readiness panel
already carries for a host that cannot answer.

So the chooser distinguishes three states — here are the models, this runtime's models come from
configuration and none are declared, and I could not ask this machine — and remains usable in all
three by accepting a written value.

### D7 — The model is recorded on the Run, beside the cost it caused

BR-011 already has the runtime report tokens and cost at Run end. Adding the model there is small
and load-bearing: a cost figure without the model that produced it cannot be compared to any other
cost figure, which makes the whole usage record much less useful than it looks.

## Risks / Trade-offs

**The enumeration costs a sandbox.** Mitigated by D3's cache. The risk is a cache that is too
clever; the answer is to key it on what the list depends on and let it expire.

**Two mechanisms is a concept the user must hold.** The product tells them which one is in play
rather than hiding it, because a chooser that silently behaves differently per runtime is worse
than one that explains.

**Model and runtime can disagree.** Accepted, per D5. The alternative is coupling the two fields so
changing a runtime silently rewrites a model, which loses the Admin's stated intent.

**Claude's configured list can go stale.** It is the operator's list, and the measurement in the
issue is the standing warning about why. This is strictly better than the same list in code.

## Migration Plan

Two additive migrations, both nullable, both no-ops for existing rows. A deployment that sets
nothing behaves exactly as it does today — `Agents:OpenCode:Model` keeps meaning what it means, and
Claude keeps launching with no model flag until somebody chooses one.

## Open Questions

- Should the deployment default be per runtime (`Agents:<Runtime>:Model`) rather than the single
  opencode key it is today? Implementation will show whether Claude needs a default at all, or
  whether "unset means the CLI's own default" is the better answer for it.
- Where the carried session changes the offer, should the chooser say so? It is the difference
  between "these are the models" and "these are the models **your seat** reaches", and only the
  second is true in a session-carrying habitat.
