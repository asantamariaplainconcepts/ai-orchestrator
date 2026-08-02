# connector-configuration — delta for connector-form-essentials

## ADDED Requirements

### Requirement: configuring a Connector asks its essentials first

The Connector form SHALL present four inputs before anything else — the vendor, the two
coordinates, and one credential — because those are what the API requires of every Connector.
Every other input SHALL sit behind one explicit **Advanced** disclosure: the prompts directory,
the code source, and the code repository where the vendor has one.

Each input's explanation SHALL sit beside that input. Explanatory text SHALL NOT be pooled at the
end of the form, where the field it describes is off-screen on a phone.

The disclosure SHALL open by itself when the stored Connector already carries any value it
holds — a value the Admin is about to resend SHALL never be hidden from them.

**A disclosure SHALL NOT hide a field the API can require.** While the local-folder code source is
selected, its path is required and absolute server-side; the disclosure SHALL therefore stay open
and SHALL state why it cannot be collapsed. A save that fails against an invisible field is the
failure this rule forecloses.

#### Scenario: a first connect asks four questions

- **WHEN** the form opens for a project with no Connector
- **THEN** the vendor, both coordinates and one credential input are visible, and every other
  input is behind the Advanced disclosure

#### Scenario: a stored advanced value is not hidden

- **WHEN** the form opens for a Connector that stores a prompts directory, a code repository, or a
  non-default code source
- **THEN** the Advanced disclosure is already open

#### Scenario: a required field cannot be folded away

- **WHEN** the local-folder code source is selected
- **THEN** the folder path is visible and the disclosure cannot be collapsed, stating why

#### Scenario: a hint sits with its field

- **WHEN** any input carrying an explanation is rendered
- **THEN** the explanation is beside that input, not at the end of the form

### Requirement: the credential is one input, and the two paths are exclusive by construction

The form SHALL show one credential input — pasting a token — with a plain control to name an
existing secret instead. Choosing the other path SHALL **swap** the input and discard the value of
the one it replaced, so the two SHALL never both carry a value.

The API refuses a request carrying both (its exclusive-or rule); that refusal SHALL be
unreachable from the portal, because the form cannot compose such a request. Leaving the
credential blank while editing an existing Connector SHALL continue to mean "keep the stored one".

#### Scenario: swapping discards rather than accumulates

- **WHEN** an Admin switches between pasting a token and naming a secret
- **THEN** only the newly chosen input carries a value, and the other is empty

#### Scenario: the exclusive-or refusal is unreachable

- **WHEN** the form submits in either credential mode
- **THEN** the request carries exactly one of the two, never both

### Requirement: a field the code source makes inapplicable is cleared, not merely hidden

Where the code source is a local folder, the code repository names where to open a pull request —
and a local Run opens none; it leaves a branch. The form SHALL NOT render that input, SHALL state
once why it does not apply, and SHALL send the field as null.

Hiding and clearing SHALL be the same act. The API permits the combination, so a hidden input
whose stale value still travelled would persist configuration nobody can see and nothing would
refuse it.

#### Scenario: a local code source clears the code repository

- **WHEN** a Connector is saved with the local-folder code source
- **THEN** the code repository input is absent from the form and the request sends it as null

#### Scenario: switching back restores the field

- **WHEN** the code source returns to the repository
- **THEN** the code repository input is rendered again, carrying what the Connector holds
