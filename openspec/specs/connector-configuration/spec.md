# connector-configuration Specification

## Purpose
TBD - created by archiving change github-connector-backlog-mirror. Update Purpose after archive.
## Requirements
### Requirement: a project has at most one Connector

A Project SHALL have zero or one Connector, identifying the vendor and the repository coordinates
its Stories are read from. Configuring a Connector on a Project that already has one SHALL replace
it, not add a second.

#### Scenario: first configuration

- **WHEN** an Admin configures a GitHub Connector on a Project that has none
- **THEN** the Project has exactly one Connector, and its Stories can be polled

#### Scenario: reconfiguration

- **WHEN** an Admin configures a Connector on a Project that already has one
- **THEN** the Project still has exactly one Connector, carrying the new coordinates

### Requirement: the credential is verified before the Connector is stored

Saving a Connector SHALL perform live calls to the vendor using the supplied credential, and SHALL
store the Connector only if they succeed. What is verified SHALL be **every capability this
project's configuration will exercise** — not a fixed pair. Listing the repository's Stories and
reading a document are always among them; the writes the configuration will use are too. A
credential that can do one and not another SHALL NOT be accepted, because a permission missing at
save is a Run failing in front of somebody who did not configure it.

**The capability set SHALL follow the configuration.** A project whose code source is a local
folder SHALL NOT have the code capabilities verified or required: its working copy is the host's
own and git runs with the host's credentials, so nothing will clone, push or open a pull request
with this credential. An unrequested permission SHALL NOT be reported as a missing one.

Verification SHALL be read-only: no label, comment, branch, file or pull request is created or
modified by it, in any habitat. A write capability SHALL therefore be verified by asking the
vendor what the credential may do, never by doing it.

Where a vendor cannot answer that question without acting, the capability SHALL be reported
**not verifiable**, carrying the reason — and saving SHALL be allowed. An unanswerable question is
not a refusal, and reporting it as a pass would manufacture confidence nobody earned.

A failure SHALL be reported as RFC 7807 ProblemDetails naming **which capability** was refused and
carrying the vendor's own reason for it. The report SHALL distinguish four causes, because they
have four different fixes: an unreachable vendor, an unknown repository, a rejected credential, and
a credential the vendor refused for lack of permission. A vendor that answered SHALL NOT be
reported as unreachable.

Absence SHALL NOT be read as refusal: a document path that does not exist SHALL satisfy the
document capability, because "this path is empty" and "you may not look" are different answers and
only the second is a refusal.

#### Scenario: a credential that cannot read Stories is refused

- **WHEN** a Connector is saved with a credential the vendor refuses for the Stories read
- **THEN** it is not stored, and the refusal names that capability with the vendor's reason

#### Scenario: a credential that cannot write what the configuration needs is refused

- **WHEN** a Connector whose configuration will write labels is saved with a credential lacking
  that permission
- **THEN** it is not stored, and the refusal names that capability — rather than the Connector
  being stored and the permission being discovered inside a Run

#### Scenario: a local code source does not require code permissions

- **WHEN** a Connector with a local-folder code source is verified
- **THEN** the clone, push and pull-request capabilities are neither required nor reported missing

#### Scenario: an unanswerable capability is reported, never assumed

- **WHEN** the vendor cannot say whether a write is permitted without performing it
- **THEN** that capability is reported not verifiable with its reason, the Connector is stored,
  and nothing was written to verify it

#### Scenario: verification writes nothing

- **WHEN** any credential is verified, in any habitat
- **THEN** no label, comment, branch, file or pull request has been created or modified

### Requirement: the product states the permissions it needs

The product SHALL state which permissions a credential needs for **this project's configuration**,
in the vendor's own vocabulary — the names a person selects while minting a token, not the
product's internal capability names. The statement SHALL appear where a credential is supplied,
and the same list SHALL be documented where somebody minting one will look.

The list SHALL be derived from the same capability set verification uses, so a capability cannot
exist without saying what to grant for it, and the documentation cannot drift from the code.

#### Scenario: the form says what to grant

- **WHEN** an Admin supplies a credential
- **THEN** the permissions this configuration requires are stated in the vendor's own vocabulary

#### Scenario: a local code source asks for less

- **WHEN** the configuration's code source is a local folder
- **THEN** the stated permissions exclude cloning, pushing and opening pull requests

### Requirement: only a secret name is persisted

The stored Connector SHALL contain the **name** of the secret holding its access token, and the
time that secret was last set. The token value SHALL NOT be written to the Connector's row, to
logs, to telemetry, or to any API response (BR-010, as revised by DEC-052).

Where the habitat has no managed vault, stored values SHALL be protected at rest with the
framework's own data protection, held outside the application database, with the key material
held apart from the values, so possession of any one of the three does not yield a usable
credential. No API, page or log SHALL expose a stored value by any route — the store SHALL offer
no operation that reads one back.

#### Scenario: inspecting storage

- **WHEN** the Connector row is read directly from the database
- **THEN** it contains a secret name and no token value

#### Scenario: reading a Connector back through the API

- **WHEN** a client fetches a Project's Connector
- **THEN** the response carries the coordinates, the secret name and when it was last set, never
  the token

#### Scenario: a stolen store yields no credential

- **WHEN** a habitat stores values locally and those values are read without its key material
- **THEN** what is stored is not the token and no token can be recovered from it

### Requirement: secrets resolve through one seam

Token values SHALL be obtained through a single resolver abstraction. Application code SHALL NOT
read secrets from configuration directly, so the storage mechanism can change without touching
call sites.

Storing a value SHALL be a separate abstraction from resolving one, so that the ability to write
a credential is visible in the dependencies of the few places that hold it. The storing
abstraction SHALL expose no operation that returns a stored value.

#### Scenario: swapping the store

- **WHEN** the resolver implementation changes from the development store to a managed secret store
- **THEN** no calling code changes

#### Scenario: the named secret is missing

- **WHEN** a Connector names a secret the resolver cannot find
- **THEN** the operation fails with a message naming the missing secret, and never falls back to
  an empty or default credential

#### Scenario: reading and writing are different dependencies

- **WHEN** a component that only consumes credentials is inspected
- **THEN** it depends on the resolving abstraction alone and cannot store a value

### Requirement: secrets resolve per read, not at startup

Secret values SHALL be fetched when they are needed. The system SHALL NOT depend on the set of
secrets being known at process start, because Connectors — and therefore secret names — are
created while the application is running.

#### Scenario: a secret created after startup

- **WHEN** an Admin configures a Connector naming a secret that was created after the application
  started
- **THEN** it resolves without restarting the application

#### Scenario: a rotated secret

- **WHEN** a secret's value is rotated in the store
- **THEN** the next resolution uses the new value, with no restart and no cache to invalidate

### Requirement: a typed secret name answers whether it resolves

While a Connector's secret name is being typed, the product SHALL answer whether that name
resolves on this deployment — `GET
/api/projects/{id}/connector/secret-resolves?name=` — through the same seam every real
resolution uses, so the preview and the poller's first read cannot disagree. The answer SHALL
be existence only: one boolean, never the value, whatever the verdict. On the self-host
posture the form SHALL render the exact environment line the name implies
(`Secrets__<name>=`) beside the field, and SHALL warn — without blocking — when the typed
name is shaped like a token rather than a name (BR-010's split, said where it is typed).

#### Scenario: a name that resolves to nothing says not yet

- **WHEN** the typed name resolves to no value on this deployment
- **THEN** the field says so and names the remedy, before anything is saved

#### Scenario: the answer carries no value

- **WHEN** the typed name resolves
- **THEN** the response says only that it does; the value appears nowhere in it

#### Scenario: the token pasted where the name goes is caught

- **WHEN** the typed name matches a vendor token shape
- **THEN** the form warns that the name goes here and the value in the environment, and still
  allows the save

### Requirement: the host owns secret-store wiring, not the modules

Registration of any secret-store client SHALL happen in the host composition root. A module SHALL
depend only on the resolver abstraction, so modules remain host-agnostic and can be composed by
any host.

#### Scenario: a module stays host-agnostic

- **WHEN** a module needs a credential
- **THEN** it depends on the resolver abstraction only, and references no cloud SDK or hosting
  integration

### Requirement: every Connector's health is visible from the projects list

The product SHALL expose each configured Connector's health — project, vendor, last successful
sync, last failure — in one read, and the projects list SHALL show each project in one of four
states: healthy, failing, never synced, or not configured. The failure sentence SHALL be
reachable without leaving the list, and a healthy Connector SHALL show how old its last sync is.
No new probing SHALL exist: the view renders what the poller already records (BR-008).

#### Scenario: four states, four projects

- **WHEN** projects exist with a healthy, a failing, a never-synced and no Connector
- **THEN** the list shows each distinctly

#### Scenario: the failure explains itself in place

- **WHEN** a Connector is failing
- **THEN** its stored failure sentence is readable from the list

#### Scenario: recovery needs no action

- **WHEN** a failing Connector's next poll succeeds
- **THEN** the list reflects healthy on its ordinary refresh

### Requirement: a project can be retired without losing what its agents did

A Project SHALL be archivable and restorable, recording when it was archived. An archived Project
SHALL begin no new work: its Connector SHALL NOT be polled, a trigger label on its Stories SHALL
NOT create a Run, and a manual Run SHALL be refused with the reason. Work already under way SHALL
be unaffected — a Run executing when the Project is archived completes and records its outcome.
Everything already recorded SHALL remain readable at the addresses it always had. The projects
list SHALL exclude archived Projects by default while stating how many exist and offering a way
to see them. Restoring SHALL resume polling and matching with no configuration lost.

#### Scenario: archiving stops the polling

- **WHEN** an archived Project's Connector would next be polled
- **THEN** it is not polled, and nothing at the vendor changes

#### Scenario: archiving stops the matching

- **WHEN** a trigger label is applied to a Story of an archived Project
- **THEN** no Run is created

#### Scenario: archiving refuses a manual Run

- **WHEN** a Run is requested by hand on an archived Project
- **THEN** it is refused with the reason

#### Scenario: work under way is left alone

- **WHEN** a Project is archived while one of its Runs is executing
- **THEN** that Run completes and records its outcome exactly as it otherwise would

#### Scenario: the history stays readable

- **WHEN** an archived Project's Runs, their logs, or its pulse are requested
- **THEN** they are returned as they were before archiving

#### Scenario: the list says how many are hidden

- **WHEN** the projects list is read
- **THEN** archived Projects are excluded, their number is stated, and they can be shown

#### Scenario: restoring resumes the work

- **WHEN** an archived Project is restored
- **THEN** polling and matching resume, with its Connector and Automations unchanged

#### Scenario: archiving is confirmed deliberately

- **WHEN** an archive is requested without the Project's name as confirmation
- **THEN** it is refused and nothing changes

### Requirement: an Admin configures a Connector by supplying the token itself

An Admin SHALL be able to configure a Connector by supplying the access token directly, without
having created a secret beforehand. The product SHALL derive the secret's name from the project,
SHALL store the value in the habitat's secret store, and SHALL NOT ask the Admin to choose a name.
Supplying a token and naming an existing secret SHALL both remain available; both together SHALL be
refused naming the conflict.

Whether **neither** may be supplied SHALL depend on whether the project already has a Connector.
Configuring a project that has none SHALL still require one of the two, because there is nothing to
verify against. Reconfiguring a project that has one SHALL accept neither, and SHALL then resolve the
credential by that Connector's own stored secret name — so an Admin SHALL NOT have to re-supply a
credential the product already holds in order to change coordinates or settings.

The reuse path SHALL re-verify the resolved credential against the live vendor exactly as any other
configuration does, because an edit may change what the credential is being asked to read. It SHALL
NOT re-store the value, SHALL NOT return it, and SHALL NOT display it.

Reconfiguring with a **different vendor** and no new credential SHALL be refused naming why: the stored
credential belongs to the previous vendor's secret name and cannot vouch for the new one.

Storing SHALL require a caller holding the Admin role, and so SHALL the reuse path — editing
configuration behind a stored credential SHALL NOT be less protected than pasting one. A habitat whose
store cannot accept a value SHALL refuse the storing path with a reason naming what to do instead, and
the naming path SHALL continue to work there.

The Connector SHALL be persisted only after the stored value has verified against the live
vendor, so a Connector that exists is still one that works (UC-004). Supplying a new token for a
Connector that already has one SHALL replace the stored value, and subsequent Runs SHALL use the
new one without a restart.

#### Scenario: connecting without a pre-existing secret

- **WHEN** an Admin supplies coordinates and a token for a project with no Connector
- **THEN** the Connector is configured, the token is in the habitat's secret store under a name
  the product chose, and no part of the token appears in the response

#### Scenario: rotation

- **WHEN** an Admin supplies a new token for a project that already has a Connector
- **THEN** the stored value is replaced under the same name, and the next Run uses the new value

#### Scenario: the operator brings their own secret

- **WHEN** an Admin names an existing secret instead of supplying a token
- **THEN** the Connector is configured exactly as it was before this capability existed

#### Scenario: neither or both

- **WHEN** a request carries a token and a secret name together, or carries neither for a project that
  has no Connector
- **THEN** it is refused with a message naming what conflicts or what is missing

#### Scenario: a habitat that cannot store

- **WHEN** an Admin supplies a token in a habitat whose secret store cannot accept values
- **THEN** the request is refused with a reason naming what to do instead, and naming an existing
  secret still configures a Connector there

#### Scenario: a caller who is not an Admin

- **WHEN** a caller without the Admin role supplies a token
- **THEN** the request is refused and nothing is stored

#### Scenario: the token does not work

- **WHEN** the supplied token fails verification against the vendor
- **THEN** no Connector is configured, and the failure names the vendor's reason

#### Scenario: editing a setting without re-supplying the credential

- **WHEN** an Admin reconfigures an existing Connector — changing a setting or the coordinates — and
  supplies no credential
- **THEN** the stored credential is resolved by that Connector's secret name, re-verified against the
  vendor, and the configuration is saved without the value being re-stored, returned or shown

#### Scenario: an edit the stored credential cannot serve

- **WHEN** an Admin changes the owner or repository to one the stored credential cannot read
- **THEN** the refusal names the vendor's own reason and nothing is saved

#### Scenario: switching vendor without a new credential

- **WHEN** an Admin reconfigures an existing Connector to a different vendor and supplies no credential
- **THEN** it is refused naming why, because the stored credential belongs to the previous vendor

#### Scenario: reuse is not a way around the role check

- **WHEN** a caller without the Admin role reconfigures an existing Connector supplying no credential
- **THEN** the request is refused and nothing is changed

### Requirement: an Admin can test a stored Connector's credential on demand

An Admin SHALL be able to test a configured Connector's stored credential at any time, without
supplying a token and without reconfiguring anything. The test SHALL report per capability which
reads succeeded and which were refused, with the vendor's reason for each refusal.

The test SHALL use the same probe that gates saving, so the two cannot disagree about what a
working credential is. It SHALL change nothing: no write to the vendor, and a failing test SHALL
leave the stored Connector exactly as it was.

#### Scenario: a credential that still works

- **WHEN** an Admin tests a Connector whose credential can perform both reads
- **THEN** every capability is reported as succeeding

#### Scenario: a credential that has lost a permission

- **WHEN** a permission is revoked at the vendor after the Connector was stored, and an Admin tests
  it
- **THEN** the refused capability is named with the vendor's reason, and the others still report as
  succeeding

#### Scenario: testing changes nothing

- **WHEN** a test fails for any reason
- **THEN** the Connector is unchanged and nothing was written at the vendor

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


### Requirement: a Connector separates its code source from its backlog vendor

A Connector SHALL carry a code source: `Repository` (the default — the vendor's repository, as
today) or `LocalFolder` with an absolute path on the host. Stories always come from the backlog
vendor regardless of code source. Every Connector existing before this change SHALL behave as
`Repository` with no migration side effects.

#### Scenario: local folder saved in the self-host posture

- **WHEN** an Admin reconfigures a Connector with `codeSource=localFolder` and a path that
  validates as a git repository
- **THEN** the Connector persists the kind and path, and the backlog coordinates and credential
  semantics are untouched

#### Scenario: existing Connectors are unchanged

- **WHEN** the migration runs on a database with existing Connectors
- **THEN** every row reads back as `codeSource=repository` with a null path, and polling,
  labelling and dispatch behave exactly as before the change

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

### Requirement: a credential path is offered only where it can succeed

The Connector form SHALL offer a vendor credential in every deployment, because the backlog is
remote wherever the code lives: reading Stories, verifying the Connector and writing labels need
one in every posture. Only a Local Run's workspace skips a vendor credential, and that is git
rather than the backlog.

**Which of the two ways to supply it are offered SHALL follow what the deployment can do, not what
posture it is in.** Naming an existing secret SHALL always be available: a resolver is composed in
every habitat. Pasting a token SHALL be offered only where the deployment composes a secret store
that accepts writes — without one, every paste ends in the store's own refusal, and offering a
control whose only outcome is that refusal is the failure this rule removes.

Where pasting is unavailable, naming SHALL be the credential field rather than a secondary
control, and the form SHALL state the remedy the unavailable store already names, so an operator
learns how to gain the option rather than only that they lack it.

The condition SHALL come from the deployment's capabilities read — never from a client re-deriving
it from a posture, and never from provoking a refusal.

#### Scenario: a credential is always askable

- **WHEN** the Connector form renders in any deployment
- **THEN** at least one way to supply a vendor credential is offered

#### Scenario: pasting needs somewhere to put it

- **WHEN** the deployment composes no secret store that accepts writes
- **THEN** pasting is not offered, naming is the credential field, and the store's own remedy is
  stated

#### Scenario: both ways where a store exists

- **WHEN** the deployment composes a store that accepts writes
- **THEN** pasting leads and naming is available beside it, as before

#### Scenario: the posture does not decide this

- **WHEN** a self-host deployment composes a writable store
- **THEN** pasting is offered, exactly as it is in a cloud deployment
