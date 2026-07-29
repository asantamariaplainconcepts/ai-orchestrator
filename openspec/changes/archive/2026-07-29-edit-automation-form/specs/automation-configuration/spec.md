# automation-configuration

## MODIFIED Requirements

### Requirement: an Admin edits, disables and re-enables an Automation

An Admin SHALL be able to change an Automation's trigger, action, runtime, approval flag and
timeout, and to disable and re-enable it. An edit SHALL face the same BR-003 overlap validation
as a create, excluding the Automation being edited from the comparison. Enabling SHALL re-run
that validation; disabling SHALL NOT, because a disabled Automation cannot overlap anything.
Editing or disabling SHALL NOT affect Runs already active — they complete against the
Automation they were created with.

This SHALL be reachable from the portal, not only from the API. The editing surface SHALL be the same
form that creates an Automation, so that the input rules, the field vocabularies and the refusals cannot
diverge between the two.

Because the update endpoint is a full replace, an edit SHALL send every field, seeded from the
Automation's stored values — a field the form omits SHALL NOT be silently reset to its default. The
timeout SHALL therefore be a visible field in both modes: a value resent on the Admin's behalf is one
they are entitled to see.

Changing the action to one that reads no document SHALL clear the document name, because a value no
visible control can reach is a value the Admin cannot manage.

An edit SHALL NOT change whether the Automation is enabled.

#### Scenario: an edit that would overlap is refused

- **WHEN** an edit would make the trigger intersect another enabled Automation's
- **THEN** it is refused with the create-time error and nothing changes

#### Scenario: an unchanged trigger does not overlap itself

- **WHEN** an edit leaves the trigger as it was
- **THEN** it succeeds — the Automation is not compared against itself

#### Scenario: re-enabling re-checks

- **WHEN** a disabled Automation whose trigger now collides with a newer one is enabled
- **THEN** it is refused and stays disabled

#### Scenario: disabling stops future matches only

- **WHEN** an Automation with an active Run is disabled
- **THEN** the Run is unaffected and no new match is made for that trigger

#### Scenario: the form opens on what is stored

- **WHEN** an Admin opens edit on an Automation
- **THEN** every field shows that Automation's current value, including its timeout

#### Scenario: an untouched timeout survives the edit

- **WHEN** an Admin edits only the trigger label of an Automation whose timeout was set to a
  non-default value
- **THEN** the stored timeout is unchanged after the save

#### Scenario: an edit leaves the enabled flag alone

- **WHEN** a disabled Automation is edited and saved
- **THEN** it is still disabled, and an enabled one is still enabled

#### Scenario: a document name goes when its action does

- **WHEN** an Admin changes the action to one that reads no document
- **THEN** the stored document name is cleared rather than kept out of sight

#### Scenario: the refusal is the API's own

- **WHEN** a save is refused for overlap or for triggering itself
- **THEN** the reason the API gave is what the form shows
