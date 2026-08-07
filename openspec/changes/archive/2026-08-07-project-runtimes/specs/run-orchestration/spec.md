## ADDED Requirements

### Requirement: a human launch chooses the runtime for that Run only

Every human launch point — Run now, re-running a failure, launching on an open change — SHALL
pre-select the runtime the resolution chain would produce and let the human change it **for that
Run only**. The choice SHALL be recorded on the Run (BR-014) and SHALL NOT modify the Automation,
the Project default, or anything a later Run reads.

A label-triggered Run involves no human and SHALL offer no override: it executes on the resolved
runtime.

#### Scenario: the dialog opens on the resolution

- **WHEN** a Member opens Run now for a Story whose Automation has no explicit runtime
- **THEN** the dialog pre-selects the Project default, and launching without touching it records
  that resolution on the Run

#### Scenario: the choice is for that Run only

- **WHEN** a Member changes the runtime at launch and the Run executes
- **THEN** the Run records and uses the choice, and the next Run of the same Automation resolves
  as if the choice had never happened

#### Scenario: matching offers nobody a choice

- **WHEN** a label application creates a Run
- **THEN** the Run records the resolved runtime with no override involved
