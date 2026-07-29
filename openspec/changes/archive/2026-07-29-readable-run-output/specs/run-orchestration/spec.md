# run-orchestration

## ADDED Requirements

### Requirement: a Run's output reads as a transcript, not as raw events

A Run's output SHALL be presented as a transcript a person can read without parsing JSON. The agent's
own text SHALL be shown as prose through the same sanitising pipeline a Story description uses, and no
internal identifier — session, message or part — SHALL appear inside it. Each tool the agent invoked
SHALL appear as one compact line naming the tool and its subject, with the full detail one disclosure
away.

Interpretation SHALL be dialect-tolerant rather than dialect-specific: a line SHALL be treated as a JSON
object if it parses and as text if it does not, well-known fields SHALL be lifted when they are present,
and the remainder SHALL be pretty-printed. The presentation SHALL NOT branch on which runtime produced
a line, so that adding a runtime requires no change here.

A line the portal cannot interpret SHALL be shown verbatim and SHALL NOT prevent the rest from
rendering. Completeness SHALL take precedence over presentation: nothing recognised is nothing lost.

While a Run executes, a running total of tokens and cost SHALL be shown from what the lines carry, and
SHALL update as lines arrive. Absent usage SHALL read as unknown, never as zero. The Run's own recorded
usage SHALL remain authoritative once the Run has ended.

The stored transcript SHALL be unchanged by any of this: each persisted chunk SHALL still hold the exact
line the process emitted, with no schema change and no normalised event store.

The output area SHALL render its documented pattern in each of its four states — empty, loading, failed
and populated — in both themes, and SHALL be reachable by keyboard.

#### Scenario: the agent's words are prose

- **WHEN** a Run's output contains assistant text inside an event envelope
- **THEN** the text is rendered as sanitised prose, with no session, message or part identifier shown
  in it

#### Scenario: a tool invocation is one line

- **WHEN** the agent invoked a tool
- **THEN** the transcript shows one compact line naming the tool and its subject

#### Scenario: an unknown dialect degrades rather than disappears

- **WHEN** a line is a JSON object whose text field the portal does not recognise
- **THEN** the object is pretty-printed and remains fully readable

#### Scenario: a line that is not JSON at all

- **WHEN** a plain stderr line or a malformed event is rendered
- **THEN** it is shown verbatim and the rest of the transcript still renders

#### Scenario: spend while spending

- **WHEN** a Run is executing and its lines carry token counts
- **THEN** a running total of tokens and cost is shown and grows as lines arrive

#### Scenario: no usage is unknown, not free

- **WHEN** a Run's lines carry no usage at all
- **THEN** the counter reads unknown rather than zero

#### Scenario: the stored line is untouched

- **WHEN** a Run's log chunks are read from the database after any of this rendering
- **THEN** each chunk still holds exactly the line the process emitted
