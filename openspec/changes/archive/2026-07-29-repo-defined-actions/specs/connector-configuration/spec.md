# connector-configuration

## ADDED Requirements

### Requirement: a project says where its prompts live

A Connector SHALL carry the repository-relative directory that the project's prompt files live in, and
an Admin SHALL be able to change it wherever the rest of the Connector is configured (UC-004). Unset
SHALL mean `ai/prompts/`, so a project that configures nothing still resolves prompt names.

An Automation naming a repository prompt SHALL store only the file name, and the directory SHALL
resolve it. Changing the directory SHALL therefore move every such Automation at once, SHALL take
effect on each one's next Run, and SHALL require no migration — the file is read at execution time and
no copy is held.

Resolution SHALL happen in one place, owned by the module that owns the Connector, so that exactly one
site composes the path and one message can report it.

A stored name SHALL NOT escape the directory: a name that is absolute, or that traverses upward, SHALL
be refused rather than normalized. A directory that can be stepped out of would not bound anything,
and one resolution rule only holds while the other route is closed.

#### Scenario: a project that has configured nothing

- **WHEN** a repository-prompt Automation runs on a project whose prompts directory is unset
- **THEN** its name resolves against `ai/prompts/`

#### Scenario: moving the prompts is one edit

- **WHEN** an Admin changes the prompts directory on the Settings tab
- **THEN** every repository-prompt Automation on that project resolves against the new directory on
  its next Run, with no Automation edited and nothing migrated

#### Scenario: the refusal names the resolved path

- **WHEN** a prompt cannot be read
- **THEN** the failure names the directory and name it resolved to, so a misconfigured directory is
  distinguishable from a missing file

#### Scenario: a name cannot leave the directory

- **WHEN** an Automation's prompt name is absolute or traverses upward out of the prompts directory
- **THEN** it is refused, rather than resolved to a file elsewhere in the repository
