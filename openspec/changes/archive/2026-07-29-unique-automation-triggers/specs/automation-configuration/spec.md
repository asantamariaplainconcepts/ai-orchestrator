# automation-configuration

## MODIFIED Requirements

### Requirement: overlapping triggers are rejected when saved

Saving an Automation whose trigger could match a Story that an existing **enabled** Automation in
the same Project could also match SHALL fail with a domain error naming the conflicting
Automation (BR-003, DEC-033). Two triggers overlap when they share a label and either share a
state or one places no state constraint; different labels never overlap; disabled Automations are
ignored for this purpose.

**Two triggers share a label when the vendor would consider them the same label.** Comparison SHALL
be case-insensitive, for labels and for states, and the *same* comparison SHALL be used when a Story
is matched against a trigger — so a differently-cased Automation cannot be accepted and then silently
never fire.

**An exact duplicate SHALL be refused whether or not either Automation is enabled.** Two rows with the
same label and the same state are the same trigger; permitting them means the conflict surfaces later,
at enable time, to somebody who did not create it. This is distinct from subsumption, which remains
enabled-only because a disabled Automation matches nothing.

**Uniqueness SHALL be enforced by the schema, not only by the handler.** Two concurrent saves of the
same trigger SHALL result in one row and a refusal, and that refusal SHALL be the same domain error an
in-memory conflict produces — never an internal error. The constraint SHALL treat an absent state as a
value, so that two triggers with the same label and no state cannot both exist.

#### Scenario: the same label and state twice

- **WHEN** an Admin saves a second Automation with a trigger already used by an enabled one
- **THEN** the save fails and the response names the Automation it collides with

#### Scenario: the same label with different states

- **WHEN** two Automations use one label but different Story states
- **THEN** both save — no Story carries two states at once, so neither can match both

#### Scenario: a broad trigger subsumes a narrow one

- **WHEN** an Automation triggers on a label with no state constraint, and another uses the same
  label with a state
- **THEN** the save fails: a Story in that state would match both, which is the ambiguity BR-003
  exists to prevent

#### Scenario: the same label in different case

- **WHEN** an Automation triggers on `AI:Implement` and another is saved on `ai:implement`
- **THEN** the save fails, because the vendor would treat those as one label

#### Scenario: a differently-cased trigger still fires

- **WHEN** a Story is labelled `ai:implement` and an enabled Automation triggers on `AI:Implement`
- **THEN** matching fires that Automation, because the matcher compares labels as the guard does

#### Scenario: a disabled exact duplicate

- **WHEN** an Automation with a label and state exists and is disabled, and another with the same
  label and state is saved
- **THEN** the save fails, because two rows with one trigger are the same trigger regardless of
  whether either is enabled

#### Scenario: a disabled broad sibling does not subsume

- **WHEN** a disabled Automation places no state constraint on a label, and an enabled one with that
  label and a concrete state is saved
- **THEN** it is allowed, because a disabled Automation matches nothing — and enabling the disabled
  one afterwards is refused

#### Scenario: two saves at once

- **WHEN** two identical trigger saves are processed concurrently
- **THEN** exactly one Automation exists afterwards and the other caller receives the same refusal an
  in-memory conflict would have produced
