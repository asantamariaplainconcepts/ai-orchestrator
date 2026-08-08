## ADDED Requirements

### Requirement: an Automation names the model its Runs think with

An Automation SHALL carry an **optional model** beside its optional runtime. Unset means the
deployment's, resolved at execution time, so changing the deployment default changes future Runs
without touching any Automation. An Automation naming a model explicitly SHALL win over it.
Existing Automations SHALL keep behaving exactly as they do — the migration SHALL change nothing.

The form SHALL offer the model as a choice whose options come from the **selected runtime**, and
the two fields SHALL stay consistent: changing the runtime SHALL re-ask what models are available
rather than leaving an offer that belongs to the previous one.

The form SHALL distinguish three states a chooser can be in, because they mean different things to
the person reading it:

- the runtime's models were obtained and are offered;
- the runtime cannot be asked and none are declared for it in configuration;
- the machine could not be asked right now.

In every one of the three, a written value SHALL remain acceptable and leaving the field empty to
inherit SHALL remain valid. An unasked machine SHALL NOT be rendered as a runtime with no models.

#### Scenario: the deployment default applies at execution time

- **WHEN** an Automation with no explicit model fires after the deployment default changes
- **THEN** the new Run resolves to the new default, and no Automation row changed

#### Scenario: an explicit model wins

- **WHEN** an Automation naming a model fires
- **THEN** the Run executes on that model rather than the deployment's

#### Scenario: existing Automations survive the migration unchanged

- **WHEN** the schema change lands on a project with Automations
- **THEN** every existing Automation carries no model and its Runs behave exactly as before

#### Scenario: changing the runtime re-asks the question

- **WHEN** an Admin switches an Automation's runtime while editing it
- **THEN** the model choices offered are the new runtime's, and a model belonging only to the
  previous runtime is not left standing as though it were still valid

#### Scenario: a machine that cannot be asked still lets the Automation be edited

- **WHEN** the machine that runs agents cannot answer while an Admin edits an Automation
- **THEN** the form says the models could not be obtained, accepts a written value, and saves —
  never an empty list implying the runtime has none, and never a form that cannot be submitted
