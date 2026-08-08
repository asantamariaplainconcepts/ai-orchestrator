## ADDED Requirements

### Requirement: a runtime's available models are asked of the machine that will run it

A runtime SHALL declare whether it can enumerate its own models. Where it can, the product SHALL
obtain them **from the machine where its Runs execute** — this process where agents are its
children, inside a sandbox where they are not — never from the process that happens to be asking.
Where it cannot, the offered models SHALL come from configuration, so the list belongs to the
operator and changes without a release.

Neither list SHALL be held in code. A runtime that can be asked SHALL NOT be given a copied list,
and a runtime that cannot SHALL NOT be given an invented one.

Enumeration MAY be cached, and the cache SHALL be keyed on everything the answer depends on —
including the session the executing machine holds, where a habitat carries one, because the models
a seat reaches are part of the answer. A cache that outlives what it describes SHALL be treated as
a correctness fault, not a stale optimisation.

Failure to enumerate SHALL be reported as failure to ask, distinct from an empty answer.

#### Scenario: the list comes from where the agent runs

- **WHEN** a habitat executes agents in sandboxes and a runtime's models are requested
- **THEN** the models reported are the ones available inside a sandbox, which are not assumed to
  match the ones available to the process that asked

#### Scenario: a runtime that cannot enumerate reads its list from configuration

- **WHEN** models are requested for a runtime with no enumeration command
- **THEN** exactly the models configuration declares for that runtime are offered, and changing
  the configuration changes the offer with no code change

#### Scenario: an unaskable machine is not an empty answer

- **WHEN** the executing machine cannot be reached while models are requested
- **THEN** the result says the machine could not be asked, and is never reported as the runtime
  having no models

### Requirement: the resolved model reaches the agent and is recorded with its cost

Run execution SHALL resolve the model in a stated order — the human's per-Run choice recorded on
the Run, then the Automation's explicit model, then the deployment default — and SHALL pass it to
the runtime's CLI. A runtime with no resolved model SHALL launch exactly as it does today.

The model SHALL resolve independently of the runtime, and a model the runtime rejects SHALL fail
the Run naming **the model asked for and the runtime that refused it**. Nothing retries (BR-004),
so that reason is the whole message; a raw vendor error SHALL NOT be surfaced in its place.

The model the Run actually used SHALL be recorded on the Run beside the tokens and cost already
reported at run end (BR-011), because a cost figure cannot be compared to another without knowing
what produced it.

#### Scenario: the chain resolves in order

- **WHEN** a Run carries a per-Run model choice and its Automation names a different model
- **THEN** the per-Run choice wins; absent it, the Automation's; absent both, the deployment's

#### Scenario: a rejected model says which one

- **WHEN** a Run executes with a model its runtime does not have
- **THEN** the Run fails naming that model and that runtime, nothing retries, and the reason is
  not the vendor's raw error

#### Scenario: usage names what spent it

- **WHEN** a Run finishes and its usage is read
- **THEN** the model it ran on is recorded beside the tokens and cost

#### Scenario: a deployment that chooses nothing is unchanged

- **WHEN** no model is set on any Automation, Run, or configuration key beyond today's
- **THEN** every Run launches exactly as it does before this change
