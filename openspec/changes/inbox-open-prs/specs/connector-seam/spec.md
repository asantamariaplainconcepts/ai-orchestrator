## ADDED Requirements

### Requirement: the Connector lists a repository's open changes

The seam SHALL offer the open changes of a project's connected code repository — number, title,
URL, head branch name and creation time — newest first, read live from the vendor and never stored.
The member SHALL follow the seam's vocabulary: a "change", never a vendor's own word for it,
because Azure DevOps does not call it a pull request either.

A project with no connected code repository SHALL answer with an empty result rather than an
error — there is nothing to list, and that is a state, not a failure. A vendor refusal SHALL
travel as a readable reason the caller can show, following the seam's existing degradation shape.
The Azure DevOps path SHALL answer with its existing unexercised-path reason until it is exercised
for real.

#### Scenario: open changes arrive vendor-neutrally

- **WHEN** a project's connected repository has open changes
- **THEN** the seam lists them newest first with number, title, URL, head branch and creation time,
  and nothing about them is written to any store

#### Scenario: no code repository is a state

- **WHEN** the project's Connector names no code repository
- **THEN** the answer is empty and no error is raised

#### Scenario: a refusal is a reason

- **WHEN** the vendor refuses the read
- **THEN** the caller receives a readable reason instead of a list
