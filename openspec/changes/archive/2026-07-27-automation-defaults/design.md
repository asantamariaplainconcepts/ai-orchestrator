# Design: automation-defaults

## D1 — The default set is code, not data

A table in the Projects module, not a seeded row or a config section. Changing which Automations
a project starts with is a decision about how this framework is meant to be used, and it should
arrive as a reviewed commit with the reasoning attached — not as an environment variable someone
set once. It also keeps the set testable: a test can assert the four entries without a fixture.

Rejected: storing the set per organisation. There is one organisation, and a table nobody can
edit is not yet a feature worth designing.

## D2 — Idempotence comes from BR-003, not from a new check

Saving an Automation whose trigger overlaps an existing one is already refused within a project.
So applying defaults twice cannot duplicate anything, and the use case does not need its own
"already seeded" bookkeeping — the kind that goes stale the moment someone edits an Automation by
hand.

The consequence shapes the contract: **partial success is the normal outcome, not an error.** The
response reports created and skipped separately. A 409 on the second press would be technically
defensible and would make the button look broken.

## D3 — Labels are ensured through the seam, and Azure DevOps says no

`EnsureLabel(coordinates, token, name, cancellationToken)` joins the existing label writes. It is
about the *repository*, not a Story, which is a new shape for this seam — every other method
takes a story id.

GitHub creates the label if absent and treats "already exists" as success. **Azure DevOps has no
equivalent**: tags are not repository objects, they spring into existence when first applied to a
work item. Its implementation returns success without acting, with a comment saying why. The
alternative — inventing a tag by applying it to an arbitrary work item — would put a label on
somebody's backlog item to satisfy a bookkeeping urge.

That asymmetry is honest and stays visible: the response tells the Admin whether labels were
ensured, so "nothing happened" on Azure DevOps is reported rather than implied.

## D4 — A failure to label does not undo the Automations

The two writes are not one transaction, and pretending otherwise would mean rolling back
Automations because a network call failed. Automations are created first; label failures are
collected and reported. The Admin can press the button again — which is safe, by D2.

Rejected: creating labels first. A vendor outage would then leave the project with nothing,
having done the part that needs no vendor at all.

## D5 — Defaults that cost nothing and write nothing without asking

The seeded runtime is opencode's free model (DEC-044): a single click that quietly begins
spending on a paid runtime is a bad default, and the runtime is one dropdown away now that the
picker is selectable. Approval is required on `ImplementToPullRequest` alone — it is the action
that writes code and opens a pull request (DEC-040); the other three are cheap and reversible.
