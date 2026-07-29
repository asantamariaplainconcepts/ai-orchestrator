# Tasks — repo-defined-actions

- [ ] 1.1 The action in the catalogue enum, taking its path from `RubricPath` (design D1).
- [ ] 2.1 The executor reads the document live, strips leading YAML frontmatter, and uses the body as
      the prompt beside the Story context (design D2).
- [ ] 3.1 The answer is posted as a Story comment and nothing else is written (design D3).
- [ ] 4.1 Both refusals precede the agent and name the path: unreadable file, empty body (design D4).
- [ ] 5.1 The portal offers the action and its path field; not added to the seeded defaults.
- [ ] 6.1 DEC-057 recorded in DEC-048's lane (design D5).
- [ ] 7.1 Tests: the body reaches the agent verbatim; frontmatter never does; the answer becomes a
      comment; a missing path and an empty body each refuse before the agent runs; no label, state or
      pull request is written.
- [ ] 8.1 CI green; evidence on #150.
