## MODIFIED Requirements

### Requirement: a credential is either injected out of band or passed, and never silently absent

A sandbox launcher SHALL declare whether it supplies the agent's credentials out of band — a
host-side mechanism authenticating the agent's requests without the value entering the sandbox.
Where it does, the runtime SHALL NOT export credential values into the sandbox, and the Run's
transcript SHALL name that source. Where it does not, credentials SHALL travel as values for the
process's lifetime exactly as they do in-process (BR-010: values never at rest).

A launcher that declares out-of-band injection SHALL verify the credential is present before the
agent starts, and SHALL refuse the Run naming the store and the command that fixes it. An agent
SHALL NOT be started with neither an injected nor a passed credential.

**A third arrangement exists, and only in the dev loop: the owner's own session.** Where a
habitat declares session carriage, the machine's agent-CLI session state SHALL be provided to the
sandbox, the operator SHALL be able to turn it off with one setting, and the transcript SHALL
name it as the credential source — so a Run that acts and bills as somebody's seat says so. The
state SHALL be **copied** rather than mounted, so it lives exactly as long as the sandbox and an
agent cannot alter the machine's own session. The set of files carried SHALL be fixed by
observing a real CLI inside a sandbox — recorded, not assumed.

Session carriage SHALL be declared by the habitat that wants it and SHALL default off everywhere
else. A carried session is readable by whatever runs in the sandbox, which is acceptable where a
developer runs their own repositories and is not acceptable where a habitat runs somebody else's;
the consequence SHALL be stated where the option lives.

#### Scenario: the agent authenticates while holding nothing

- **WHEN** a Run executes under a launcher that injects credentials out of band
- **THEN** no credential value exists inside the sandbox, the agent's authenticated calls
  succeed, and the transcript names the injection as the credential source

#### Scenario: the injecting launcher has no stored credential

- **WHEN** a launcher declaring out-of-band injection is configured but the credential was never
  stored
- **THEN** the Run refuses before the agent starts, naming the store and the command that
  fixes it — never an unauthenticated agent failing later for an unrelated-looking reason

#### Scenario: the dev loop's Run runs as its owner

- **WHEN** a sandboxed Run executes in a habitat declaring session carriage, on a machine signed
  into the runtime's CLI, with no credential secret stored for that runtime
- **THEN** the agent authenticates as that session, the Run reaches a terminal state, and the
  transcript names the owner's session as the credential source

#### Scenario: the machine's own session is not disturbed

- **WHEN** a session-carried Run has finished
- **THEN** the machine's session state is unchanged, because the sandbox held a copy

#### Scenario: another habitat does not acquire it by forgetting

- **WHEN** a habitat that does not declare session carriage executes a sandboxed Run
- **THEN** no session state exists inside the sandbox, and credentials are injected or passed as
  before
