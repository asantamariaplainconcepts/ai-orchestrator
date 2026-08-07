# run-preview Specification

## Purpose
TBD - created by archiving change run-previews-over-published-ports. Update Purpose after archive.

## Requirements

### Requirement: a preview exists only while its Run is executing

Where an Automation names a preview port and the Run executes in a sandbox, the launcher SHALL
publish that sandbox port to an ephemeral host port on loopback for the life of the sandbox, and
SHALL record it where the portal can read it. When the agent finishes — succeeding, failing,
timing out or cancelled — the sandbox is disposed and the record SHALL go with it.

A Run in a terminal state SHALL offer no preview and no trace of one: no link, no disabled
control, and no message describing a preview that has ended. A preview SHALL NOT be recoverable,
extendable, or persisted, because a preview is not something a Run leaves behind.

#### Scenario: a live Run can be looked at

- **WHEN** a Run is executing in a sandbox with a preview port configured and something inside is
  serving
- **THEN** a Member who may see the Run can view the running application from the Run's detail

#### Scenario: a finished Run offers nothing

- **WHEN** a Run reaches any terminal state
- **THEN** its detail shows no preview affordance of any kind, and no record of the preview
  remains anywhere

#### Scenario: the preview ends while someone is watching

- **WHEN** a Run reaches a terminal state while a Member has its preview open
- **THEN** the view reports that the Run finished and offers what the Run did record, rather than
  a broken page

#### Scenario: nothing is serving yet

- **WHEN** the port is published but nothing inside the sandbox is listening
- **THEN** the view says nothing is serving yet — a state of a live Run, never an error or a
  failure of the Run

### Requirement: the preview record is per-process and never durable

The record of which Runs have published ports SHALL live in the memory of the process holding
the sandboxes, never in the database, for the reason the pod ledger states: a stored row would
outlive the sandbox it describes and lie after a restart.

A process that holds no sandboxes SHALL answer that previews are not available there, which is a
different sentence from a Run having no preview.

#### Scenario: a restart forgets what no longer exists

- **WHEN** the process holding sandboxes restarts
- **THEN** no preview records survive, matching the sandboxes that did not survive either

#### Scenario: a portal that is not the sandbox host says so

- **WHEN** the portal is a different process from the one executing Runs
- **THEN** it reports previews as unavailable in this habitat rather than implying the Run failed
  to produce one

### Requirement: relayed agent content cannot act as the portal

A published port is loopback-bound, so the portal SHALL relay it rather than link to it. The
relay SHALL be scoped to the published port of one Run and SHALL refuse any other target, and it
SHALL apply the same authorization the Run itself requires — decided at the relay, never in the
browser.

Relayed content SHALL be rendered so that it cannot reach the portal's session, its API, or its
storage as the Member, and the surface SHALL state that what is being rendered is the Run's own
application.

#### Scenario: the framed application cannot borrow the Member's authority

- **WHEN** relayed content attempts to read the portal's session or call its API as the Member
- **THEN** the attempt fails because the framing grants no such access

#### Scenario: the relay is not a general proxy

- **WHEN** a request asks the relay for any target other than the Run's own published port
- **THEN** it is refused

#### Scenario: someone who may not see the Run may not see its preview

- **WHEN** a caller without access to the Run requests its preview
- **THEN** the relay refuses, exactly as the Run's own read would
