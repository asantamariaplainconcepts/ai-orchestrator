# automation-configuration

## ADDED Requirements

### Requirement: a phase timeout is bounded, and the infrastructure honours the bound

An Automation's phase timeout SHALL be configurable by an Admin up to a ceiling the product states,
and a value above that ceiling SHALL be refused at save naming it. The ceiling exists so the platform
budget that hosts a phase can be provably sufficient: without an upper bound, "configurable" means
"configurable up to whatever the infrastructure happens to allow", which is not a promise the product
can keep.

The provisioned execution budget SHALL be at least the ceiling plus a margin for a worker to finish
writing its outcome after a phase ends. The ceiling, the provisioned budget and the business rule
SHALL each carry a reference to the other two, because no automated check can span a code constant, an
infrastructure value and a documented rule.

#### Scenario: a timeout above the ceiling

- **WHEN** an Admin saves an Automation whose phase timeout exceeds the ceiling
- **THEN** the save is refused, naming the ceiling, and nothing is stored

#### Scenario: a timeout at the ceiling

- **WHEN** an Admin saves an Automation whose phase timeout equals the ceiling
- **THEN** it is accepted

#### Scenario: the provisioned budget covers the ceiling

- **WHEN** the deployed execution budget is compared with the ceiling
- **THEN** it is at least the ceiling plus a drain margin
