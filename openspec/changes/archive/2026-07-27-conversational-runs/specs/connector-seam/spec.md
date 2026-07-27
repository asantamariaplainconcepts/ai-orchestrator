# connector-seam

## ADDED Requirements

### Requirement: a Story's comments can be read live

The seam SHALL expose the comments on a Story from a moment onwards, read live from the vendor
and never mirrored. Each comment SHALL carry its body and creation time. The read exists for
resuming conversations, so it SHALL be cheap to ask "anything since the questions?" without
paging a Story's whole history.

#### Scenario: comments since a moment

- **WHEN** comments are read with a since-timestamp
- **THEN** only comments at or after it are returned, oldest first

#### Scenario: nothing new

- **WHEN** no comment exists after the timestamp
- **THEN** the result is empty, not an error
