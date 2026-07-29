# Tasks — repo-defined-actions

- [x] 1.1 The action in the catalogue enum, taking its file name from `RubricPath` (design D1).
- [x] 2.1 The Connector carries the prompts directory — nullable column, migration, and the field
      through `ConfigureConnector` and `GetBacklog`, following `CodeRepository`'s precedent (design D6).
- [x] 2.2 Resolution behind `IDocumentReader`, inside the Backlog module: name against directory,
      defaulting to `ai/prompts/`, refusing a name that is absolute or traverses upward (design D6).
- [x] 3.1 The executor reads the document live, strips leading YAML frontmatter, and uses the body as
      the prompt beside the Story context (design D2).
- [x] 4.1 The answer is posted as a Story comment and nothing else is written (design D3).
- [x] 5.1 Both refusals precede the agent and name the **resolved** path: unreadable file, empty body
      (design D4).
- [x] 6.1 The portal offers the action with its file-name field; not added to the seeded defaults.
- [x] 6.2 The prompts directory is editable on the Settings tab beside the code repository, with its
      i18n keys and the mock updated (design D6).
- [x] 7.1 DEC-057 recorded in DEC-048's lane (design D5).
- [x] 8.1 Tests: the body reaches the agent verbatim; frontmatter never does; the answer becomes a
      comment; a missing name and an empty body each refuse before the agent runs, naming the resolved
      path; no label, state or pull request is written.
- [x] 8.2 Tests for the directory: an unset directory resolves against `ai/prompts/`; changing it moves
      an existing Automation's resolution with no Automation edited; an absolute or upward-traversing
      name is refused.
- [ ] 9.1 CI green; evidence on #150.
