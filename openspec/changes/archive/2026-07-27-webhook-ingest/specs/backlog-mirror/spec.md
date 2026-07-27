# backlog-mirror

## ADDED Requirements

### Requirement: a verified vendor webhook triggers the same reconciliation a poll does

The system SHALL accept vendor webhooks at a public endpoint, verify the request's signature
against the Connector's configured secret using a constant-time comparison, and — for an
interesting event — run the same reconciliation the poller runs, so the resulting story events
are produced by one code path and are identical whatever prompted them (BR-015). The payload
SHALL NOT be translated into a story event. An unsigned or wrongly signed request, and one
naming a repository no Connector watches, SHALL be refused indistinguishably (no existence
leak). An uninteresting event SHALL be acknowledged without work. Polling SHALL continue
regardless, so a missed webhook costs latency and never correctness. The webhook secret SHALL
be held by name (BR-010).

#### Scenario: a signed webhook reconciles

- **WHEN** a correctly signed event arrives for a watched repository
- **THEN** the mirror is reconciled exactly as a poll would reconcile it, and any story event
  is indistinguishable from a poll's

#### Scenario: an unsigned or wrongly signed request is refused

- **WHEN** the signature is absent or wrong
- **THEN** the request is refused and no reconciliation happens

#### Scenario: an unknown repository leaks nothing

- **WHEN** the payload names a repository no Connector watches
- **THEN** the answer is the same as a signature failure

#### Scenario: an uninteresting event is accepted and ignored

- **WHEN** an event the product does not act on arrives
- **THEN** the response is success and no reconciliation happens

#### Scenario: polling still reconciles without webhooks

- **WHEN** a Story changes and no webhook arrives
- **THEN** the next poll reconciles it as before
