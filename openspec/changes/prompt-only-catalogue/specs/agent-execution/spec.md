# agent-execution

## REMOVED Requirements

### Requirement: an ImplementToPullRequest Run produces a linked pull request

**Reason**: the built-in catalogue is retired (#162). A Run that opens a pull request is now something a
repository's prompt does with the workspace and the PAT it holds, not a ceremony this product performs.

**Migration**: an Automation that named this action is deleted by the migration; the same outcome is
reached by a prompt that clones, changes and pushes.

### Requirement: the grill action interrogates a Story to its project's readiness bar

**Reason**: retired with the catalogue (#162). The readiness bar was already the project's own document
(DEC-048); now the whole interrogation is the project's own prompt.

**Migration**: the Automation is deleted. The conversational resume path it drove stays in place but has
no producer until a prompt or the grants follow-up gives it one.

### Requirement: the propose action turns a ready Story into a documentation PR

**Reason**: retired with the catalogue (#162).

**Migration**: the Automation is deleted; a prompt in the project's prompts directory expresses the same
step.

### Requirement: a SyncChange Run closes the Story's change as the repository says to

**Reason**: retired with the catalogue (#162). This action already read its procedure from the repository,
which makes it the clearest case of a ceremony with nothing left to own.

**Migration**: the Automation is deleted; the procedure document becomes the prompt itself.

### Requirement: every catalogue action executes

**Reason**: there is no catalogue left to execute (#162). One action remains, and its behaviour is
described by the requirement modified below rather than by a per-action list.

**Migration**: Automations naming any of the removed actions are deleted by the migration.

## MODIFIED Requirements

### Requirement: an Automation may take its prompt from the repository

An Automation SHALL name exactly one kind of action: a markdown prompt in the connected repository. Any
other action value SHALL be refused with the unknown-action refusal.

A Run SHALL prepare a workspace cloned with the project's credential, resolve its prompt live from the
project's prompts directory, and execute the agent holding that credential and the AI credential. The
agent SHALL be free to do whatever the prompt says — comment, label, transition, push, open or merge a
pull request — exactly as the repository's own scripts would.

The orchestrator SHALL perform **no** vendor or repository write of its own after the agent runs. Success,
failure and the reply SHALL come from the agent's result; usage SHALL be reported as on any Run, and
absent usage SHALL remain unknown.

Leading YAML frontmatter SHALL still be stripped and ignored: it is another runner's wiring, and the
Automation remains this product's. Both refusals SHALL still precede the agent, naming the resolved path:
a prompt that cannot be read, and a prompt whose body is empty once frontmatter is removed.

This requirement replaces the bounded single-comment surface recorded for the repository prompt: the bound
is removed on purpose, and per-Automation grants are the named follow-up that will make bounds
expressible again.

#### Scenario: the repository's prompt is what the agent receives

- **WHEN** a Run executes an Automation naming a markdown file that exists
- **THEN** the file's body is the agent's instruction, alongside the Story's context

#### Scenario: frontmatter is not part of the prompt

- **WHEN** the named file begins with a YAML frontmatter block
- **THEN** that block does not reach the agent, and the body after it does

#### Scenario: the answer becomes a comment

- **WHEN** the prompt instructs the agent to comment on the Story
- **THEN** the **agent** posts that comment — the orchestrator no longer posts one on its behalf, and a
  prompt that says nothing about commenting produces no comment

#### Scenario: a prompt cannot grant itself powers

- **WHEN** the prompt or its frontmatter asks for tools, a model or a write
- **THEN** the frontmatter is still ignored, and the agent's powers are the ones the runtime and the
  project's credential already give it — which, until per-Automation grants land, is everything that
  credential can do

#### Scenario: the agent finishes the job itself

- **WHEN** a prompt instructs the agent to write to the vendor or the repository
- **THEN** the agent performs those writes, and the orchestrator performs none of its own afterwards

#### Scenario: the outcome is the agent's

- **WHEN** the agent's result reports success or failure
- **THEN** the Run's state follows it, and the log and usage are recorded as on any Run

#### Scenario: the file is not there

- **WHEN** the name does not resolve to a readable file in the project's prompts directory
- **THEN** the Run fails naming the resolved path, before any agent runs

#### Scenario: the file says nothing

- **WHEN** the named file's body is empty once frontmatter is stripped
- **THEN** the Run fails naming the resolved path, before any agent runs, rather than sending an empty
  prompt

#### Scenario: any other action is unknown

- **WHEN** an Automation is saved or executed with any action other than the repository prompt
- **THEN** it is refused as an unknown action
