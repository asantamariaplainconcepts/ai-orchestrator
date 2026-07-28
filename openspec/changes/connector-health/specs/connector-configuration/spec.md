# connector-configuration

## ADDED Requirements

### Requirement: every Connector's health is visible from the projects list

The product SHALL expose each configured Connector's health — project, vendor, last successful
sync, last failure — in one read, and the projects list SHALL show each project in one of four
states: healthy, failing, never synced, or not configured. The failure sentence SHALL be
reachable without leaving the list, and a healthy Connector SHALL show how old its last sync is.
No new probing SHALL exist: the view renders what the poller already records (BR-008).

#### Scenario: four states, four projects

- **WHEN** projects exist with a healthy, a failing, a never-synced and no Connector
- **THEN** the list shows each distinctly

#### Scenario: the failure explains itself in place

- **WHEN** a Connector is failing
- **THEN** its stored failure sentence is readable from the list

#### Scenario: recovery needs no action

- **WHEN** a failing Connector's next poll succeeds
- **THEN** the list reflects healthy on its ordinary refresh
