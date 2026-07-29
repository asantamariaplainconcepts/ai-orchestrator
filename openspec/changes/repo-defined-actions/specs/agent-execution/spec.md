# agent-execution

## ADDED Requirements

### Requirement: an Automation may take its prompt from the repository

An Automation SHALL be able to name a markdown file in the connected repository as its action, and a
Run of that Automation SHALL use the file's content as the agent's instruction. The file SHALL be read
live at execution time and SHALL NOT be mirrored or cached, so the repository remains the only copy.

Leading YAML frontmatter SHALL be stripped and ignored. That block is how another runner is told what
to do with the file, while this product's wiring is the Automation itself — its runtime, timeout,
approval gate and trigger. Ignoring it SHALL be deliberate: a declared model SHALL NOT choose what
this product spends, and a declared tool list SHALL NOT grant powers the Automation did not give.

The write surface SHALL be one comment on the Story and nothing else: no label, no state, no
workspace, and no pull request. A repository prompt SHALL NOT be able to widen its own surface by
asking to.

Both refusals SHALL precede the agent, each naming the path: a file that cannot be read, and a file
whose body is empty once frontmatter is stripped. There SHALL be no fallback prompt and no substituted
catalogue action — an Automation configured to run the repository's prompt SHALL either run it or stop.

Usage, cost and streamed output SHALL behave as on any other Run.

#### Scenario: the repository's prompt is what the agent receives

- **WHEN** a Run executes an Automation naming a markdown file that exists
- **THEN** the file's body is the agent's instruction, alongside the Story's context

#### Scenario: frontmatter is not part of the prompt

- **WHEN** the named file begins with a YAML frontmatter block
- **THEN** that block does not reach the agent, and the body after it does

#### Scenario: the answer becomes a comment

- **WHEN** the agent answers successfully
- **THEN** the answer is posted as a comment on the Story, and no label, state or pull request is
  written

#### Scenario: the file is not there

- **WHEN** the named path cannot be read from the repository
- **THEN** the Run fails naming the path, before any agent runs

#### Scenario: the file says nothing

- **WHEN** the named file's body is empty once frontmatter is stripped
- **THEN** the Run fails naming the path, before any agent runs, rather than sending an empty prompt

#### Scenario: a prompt cannot grant itself powers

- **WHEN** the named file's frontmatter or body asks for tools, a model, or a write the Automation did
  not configure
- **THEN** nothing about the Run's surface changes: one comment, the Automation's runtime, the
  Automation's timeout
