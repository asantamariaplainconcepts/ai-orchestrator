# agent-execution

## ADDED Requirements

### Requirement: every catalogue action executes

Run execution SHALL dispatch on the Automation's action, and all four of DEC-026's actions SHALL
execute: implement-to-pull-request (unchanged), refine-or-comment (the Agent's answer posted as
a Story comment), transition-state (the Agent's proposed state written through the seam), and
estimate (an `estimate:<n>` label replacing any prior one, plus the reasoning as a comment).
Only implement-to-pull-request SHALL prepare a workspace — the others touch no code. An
estimate whose answer carries no number, and a transition whose state the vendor rejects, SHALL
fail the Run with that reason rather than guessing.

#### Scenario: the Agent comments

- **WHEN** a refine-or-comment Run executes
- **THEN** the Agent's answer is a comment on the Story and the Run succeeds

#### Scenario: the Agent transitions

- **WHEN** a transition-state Run executes and the Agent names an acceptable state
- **THEN** the Story's state changes and the Run succeeds

#### Scenario: the Agent estimates

- **WHEN** an estimate Run executes
- **THEN** the Story carries exactly one `estimate:<n>` label and a comment with the reasoning

#### Scenario: an unusable answer fails honestly

- **WHEN** an estimate answer carries no number, or a transition names a state the vendor
  rejects
- **THEN** the Run fails with that reason and the Story is unchanged

#### Scenario: only the PR action clones

- **WHEN** any action other than implement-to-pull-request executes
- **THEN** no workspace is prepared and nothing is published
