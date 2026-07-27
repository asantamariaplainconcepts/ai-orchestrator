# Design — azure-devops-connector

## D1 — The seam does not change, and that is the result being reported

Every method Azure DevOps implements was designed for GitHub. If the second vendor had needed
the seam widened, the seam would have been a GitHub abstraction wearing a neutral name. It did
not: tags answer labels, work items answer stories, `System.State` answers state. The one thing
that did not fit — code living apart from the backlog — is handled by an *optional field on the
Connector*, not by changing a single seam signature.

## D2 — HYPOTHESIS (ADR-0005): none of this has met a real Azure DevOps

There was no organisation to exercise against. What is verified: the translation (unit tests
over tag parsing, state mapping, estimate-field selection and error translation) and the seam
contract (the stub-driven functional tier). What is not: that the REST calls, field names and
authentication behave as documented. First thing to try when an organisation exists: configure
a Connector against a project, hit refresh, and confirm the Mirror fills — that single path
exercises authentication, the work-item query, and tag/state/description translation at once.

## D3 — Process-dependent fields are refused, never guessed

Agile projects estimate in `Microsoft.VSTS.Scheduling.StoryPoints`, Scrum in `Effort`, Basic in
nothing at all; state vocabularies differ the same way. A connector that picked one would be
right for a third of installations and silently wrong for the rest. So: the state the Agent
named is sent and the vendor's rejection is surfaced (exactly the shape GitHub's two-state
vocabulary already produces), and the estimate tries the known fields in order and fails naming
them when none applies. "This project has no estimate field" is a useful sentence; a silently
skipped estimate is not.

## D4 — Tags are a delimited string, and that is a translation detail

`System.Tags` is a single semicolon-delimited string, not a collection. Splitting and rejoining
happens inside the connector; nothing outside learns that Azure DevOps models tags this way.
The same containment reasoning as Octokit's issue types.

## D5 — The code repository is optional because only one vendor needs it

Adding a required field would make every GitHub Connector carry a value that means nothing.
Adding a second Connector per project would double the configuration surface for a distinction
one vendor has. An optional field, ignored by the vendor that does not need it, is the smallest
honest shape.
