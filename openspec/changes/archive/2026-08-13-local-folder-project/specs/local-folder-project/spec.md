## ADDED Requirements

### Requirement: a Project is added by naming a folder on this machine

In the self-host posture, creating a Project SHALL accept an optional absolute folder path
alongside the Project's name, and the Project that results SHALL already carry its Connector
coordinates and the `LocalFolder` code source. The operator SHALL NOT have to configure a Connector
as a second step to reach a working Project.

The folder SHALL be inspected **inside the create command's own handler**, through the existing
`ILocalCodeWorkspace` seam
(`src/shared/AiOrchestrator.BuildingBlocks/Agents/ILocalCodeWorkspace.cs`). **No new HTTP surface
SHALL be added.** `POST /api/projects/{projectId}/connector/validate-path` SHALL stay
project-scoped and unchanged, so no filesystem read becomes reachable without an existing Project to
authorize against (BR-009).

Every derived value SHALL be editable before saving — derivation is a starting point the operator
may correct, never a value they are stuck with.

#### Scenario: naming a folder yields a configured Project

- **WHEN** an Admin creates a Project in a self-host deployment naming a folder whose `origin` is a
  recognised remote
- **THEN** the Project exists with its vendor, owner and repository set from that remote and its
  code source set to the folder, with no second configuration step

#### Scenario: no filesystem surface is added

- **WHEN** the create request inspects the named folder
- **THEN** the inspection happens through `ILocalCodeWorkspace` inside the handler, and
  `validate-path` remains project-scoped and unchanged

#### Scenario: a derived value can be corrected

- **WHEN** an Admin reviews the coordinates derived from a folder
- **THEN** each of them is editable before the Project is saved

### Requirement: the folder's remote names the vendor and the coordinates

Where the named folder is a git repository with an `origin` remote, the product SHALL derive the
vendor and its coordinates from that remote, for **both** vendors and **both** remote forms.

- A GitHub remote SHALL yield Vendor GitHub with Owner and Repository from the remote.
- An Azure DevOps remote — `dev.azure.com/{org}/{project}/_git/{repo}` or
  `{org}.visualstudio.com/{project}/_git/{repo}` — SHALL yield Vendor Azure DevOps with Owner
  `{org}`, Repository `{project}` and Code repository `{repo}`: the three fields
  `AzureDevOpsBacklogConnector` actually reads, in the shape it reads them.
- The SSH and the HTTPS form of the same remote SHALL yield identical coordinates.

#### Scenario: a GitHub remote in either form

- **WHEN** the folder's `origin` is `git@github.com:owner/repo.git` or
  `https://github.com/owner/repo.git`
- **THEN** both yield Vendor GitHub, Owner `owner` and Repository `repo`

#### Scenario: an Azure DevOps remote in either form

- **WHEN** the folder's `origin` is the SSH or the HTTPS form of
  `dev.azure.com/contoso/Platform/_git/api`
- **THEN** both yield Vendor Azure DevOps, Owner `contoso`, Repository `Platform` and Code
  repository `api`

#### Scenario: the legacy Azure DevOps host

- **WHEN** the folder's `origin` is `https://contoso.visualstudio.com/Platform/_git/api`
- **THEN** it yields the same coordinates as the `dev.azure.com` form

### Requirement: a folder that answers nothing says which check failed

Where the named folder is not a directory, is not a git repository, has no `origin`, or has an
`origin` matching neither vendor, the coordinate fields SHALL be left empty and editable and the
response SHALL name **which of those four checks failed**. The Admin types the coordinates manually
and the flow proceeds — a folder that cannot answer SHALL NOT block creating a Project.

#### Scenario: each failure is named, not generic

- **WHEN** a folder is named that is not a directory, or is not a git repository, or has no
  `origin`, or has an `origin` matching neither vendor
- **THEN** the response names which of those four it was, and the coordinates are empty and editable

#### Scenario: an unanswerable folder still allows a Project

- **WHEN** an Admin proceeds after any of those four failures, typing the coordinates
- **THEN** the Project is created with the typed coordinates

### Requirement: the folder step exists only where a folder means anything

The folder input SHALL be composed only in the self-host posture — the same discriminator that
composes the rest of the code-source surface (`local-code-source`). A governed deployment SHALL NOT
offer it, exactly as the code source is absent from the Connector form there today, because a path
naming a cloud container's own disk is a trap rather than a feature (DEC-049).

The portal SHALL decide this from the deployment capabilities read, never by re-deriving a posture
on the client.

#### Scenario: a cloud deployment has no folder step

- **WHEN** the add-Project form renders in a governed deployment
- **THEN** no folder input is offered and creating a Project behaves exactly as it does today

#### Scenario: a folder sent to a deployment that has no such step

- **WHEN** a create request carrying a folder reaches a deployment composed without the self-host
  posture
- **THEN** it is refused rather than silently ignored, and no Project gains a local path
