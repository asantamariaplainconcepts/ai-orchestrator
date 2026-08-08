## Why

[#291](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/291). The runtime an
Automation uses is chosen at three levels and has been for a while — the per-Run choice, then the
Automation's, then the Project default, then the deployment's. The **model** is chosen at none. It
is a single process-wide configuration key, `Agents:OpenCode:Model`, defaulting to
`opencode/deepseek-v4-flash-free` (DEC-044), and Claude Code is launched with no model flag at all.

So every Run in a deployment thinks with the same brain. An Admin cannot give a hard Automation a
better model or a trivial one a cheaper one, and a Member launching *Run now* on something thorny
cannot raise the bar for that one attempt. Model is the cost-and-quality lever — BR-011 has the
product report tokens and cost at every Run end, and today it reports them for a choice nobody made.

Actors: **ACT-001 Admin** sets it on the Automation; **ACT-002 Member** overrides it at launch.
Use cases: UC-005, UC-006, UC-012, UC-020. Business rules: BR-004, BR-005, BR-011.

## What Changes

An Automation carries an optional model, resolved at execution time exactly like its runtime, and a
human launch may override it for that Run only. Absent everywhere, behaviour is what it is today.

Where the choice comes from differs per runtime, because **the CLIs genuinely differ and pretending
otherwise would ship a lie**:

- **opencode can be asked.** `opencode models` answers with one model per line. So the product asks
  it, on the machine that will actually run the agent.
- **Claude Code cannot.** It accepts `--model` but has no command that lists models. So its offer
  comes from configuration, `Agents:<Runtime>:Models` — the operator's list, editable without a
  release.

The Run records the model it ran on, beside the tokens and cost it already records, because a cost
figure is uninterpretable without knowing what spent it.

## Capabilities

### New Capabilities

None. Every requirement here extends behaviour that already has a home.

### Modified Capabilities

- `automation-configuration`: an Automation carries an optional model beside its optional runtime,
  and the form's offer is built from what the selected runtime can actually be asked.
- `agent-execution`: the model resolves in a stated order, reaches the CLI, is recorded on the Run,
  and a model the runtime rejects fails naming it.
- `run-orchestration`: a human launch chooses the model for that Run only, exactly as it already
  chooses the runtime.

## Impact

**Domain and persistence.** `Automation` gains a nullable model; `Run` gains a nullable model and a
recorded resolved model. Two migrations, both additive; existing rows keep behaving as they do.

**Execution.** `RunExecutor`'s resolution gains a second axis beside the runtime's.
`OpenCodeRuntime` already passes `-m`; it stops reading a singleton option and takes the resolved
value. `ClaudeCodeHeadlessRuntime` gains `--model`, which it has never passed.

**Discovery.** A new read that asks a runtime for its models, answered where agents run — which in
a sandboxed habitat means inside a sandbox, and therefore composes with the session carriage that
just landed (#288): the models an owner's seat can reach are the ones the carried session unlocks.

**UI.** The Automation form and every human launch dialog gain a model field beside the runtime
they already have; both are in the Platform-theme half of DEC-051.

**Configuration.** `Agents:<Runtime>:Models` is new. `Agents:OpenCode:Model` keeps its meaning as
the deployment default and is not removed.

**Not touched.** Per-project model defaults, provider selection separate from model, automatic
routing, spend caps, and changing an in-flight Run's model are all out of scope.
