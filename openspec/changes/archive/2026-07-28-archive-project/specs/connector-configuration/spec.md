# connector-configuration

## ADDED Requirements

### Requirement: a project can be retired without losing what its agents did

A Project SHALL be archivable and restorable, recording when it was archived. An archived Project
SHALL begin no new work: its Connector SHALL NOT be polled, a trigger label on its Stories SHALL
NOT create a Run, and a manual Run SHALL be refused with the reason. Work already under way SHALL
be unaffected — a Run executing when the Project is archived completes and records its outcome.
Everything already recorded SHALL remain readable at the addresses it always had. The projects
list SHALL exclude archived Projects by default while stating how many exist and offering a way
to see them. Restoring SHALL resume polling and matching with no configuration lost.

#### Scenario: archiving stops the polling

- **WHEN** an archived Project's Connector would next be polled
- **THEN** it is not polled, and nothing at the vendor changes

#### Scenario: archiving stops the matching

- **WHEN** a trigger label is applied to a Story of an archived Project
- **THEN** no Run is created

#### Scenario: archiving refuses a manual Run

- **WHEN** a Run is requested by hand on an archived Project
- **THEN** it is refused with the reason

#### Scenario: work under way is left alone

- **WHEN** a Project is archived while one of its Runs is executing
- **THEN** that Run completes and records its outcome exactly as it otherwise would

#### Scenario: the history stays readable

- **WHEN** an archived Project's Runs, their logs, or its pulse are requested
- **THEN** they are returned as they were before archiving

#### Scenario: the list says how many are hidden

- **WHEN** the projects list is read
- **THEN** archived Projects are excluded, their number is stated, and they can be shown

#### Scenario: restoring resumes the work

- **WHEN** an archived Project is restored
- **THEN** polling and matching resume, with its Connector and Automations unchanged

#### Scenario: archiving is confirmed deliberately

- **WHEN** an archive is requested without the Project's name as confirmation
- **THEN** it is refused and nothing changes
