## ADDED Requirements

### Requirement: a human launch chooses the model for that Run only

Every human launch point that already offers the runtime — Run now, re-running a failure, launching
on an open change — SHALL also pre-select the model the resolution chain would produce and let the
human change it **for that Run only**. The choice SHALL be recorded on the Run and SHALL NOT modify
the Automation or anything a later Run reads.

The models offered SHALL be those of the runtime selected **in that dialog**, so choosing a runtime
and then a model reads as one decision rather than two that can silently disagree.

A label-triggered Run involves no human and SHALL offer no override: it executes on the resolved
model.

#### Scenario: the dialog opens on the resolution

- **WHEN** a Member opens Run now for a Story whose Automation names no model
- **THEN** the dialog pre-selects the deployment default, and launching without touching it
  records that resolution on the Run

#### Scenario: the choice is for that Run only

- **WHEN** a Member changes the model at launch and the Run executes
- **THEN** the Run records and uses the choice, and the next Run of the same Automation resolves
  as if the choice had never happened

#### Scenario: the runtime chosen in the dialog decides the models offered

- **WHEN** a Member changes the runtime in the launch dialog
- **THEN** the models offered become that runtime's, and a model belonging only to the previously
  selected runtime is not left standing

#### Scenario: matching offers nobody a choice

- **WHEN** a label application creates a Run
- **THEN** the Run records the resolved model with no override involved
