## Context

An Automation's whole behaviour is the prompt it names (#162), and the name is typed blind into a
text field (#150). The project's prompts directory is Connector configuration (Backlog module),
the repository is reachable through the Connector with the project PAT (BR-010), and the seam
already reads single documents live at a ref. What is missing is one read (a directory listing)
and one form affordance (a picker that suggests without restricting). The output-label picker
(#165) already established the pattern this field should follow: suggest, never gate.

## Goals / Non-Goals

**Goals:**
- The Admin sees which prompts actually exist while wiring an Automation, without leaving the form.
- Discovery reads live truth (default branch, at that moment) — no cache to go stale, no mirror.
- Discovery can fail without taking configuration down with it.

**Non-Goals:**
- Validating or previewing prompt content in the form (the Run path's frontmatter/body rules are
  the only content authority).
- Restricting saves to listed names — free text is a requirement, not a fallback.
- Listing branches other than the default.
- Changing save-side validation or the Run path's missing-prompt refusal.

## Decisions

**D1 — the listing is a seam read on `IBacklogConnector`, beside the document read.** One method:
list the entries of a repository directory at the default branch, returning file names only (the
picker's unit is a name relative to the prompts directory, exactly what the Automation stores).
Vendor-neutral types; no vendor noun crosses the seam. GitHub implements it with the contents API;
Azure DevOps beside it per ADR-0005 — written, unit-tested in translation, labelled a hypothesis.
*Alternative rejected:* cloning the repo to list a folder — a workspace exists for Runs, not for a
form keystroke; the PAT-scoped API read is cheaper and needs no disk.

**D2 — the Backlog module exposes the listing; the portal asks it, not GitHub.** A query use case
(`GET /api/projects/{projectId}/prompts`) resolves the project's Connector, reads its prompts
directory setting, resolves the PAT by name at the moment of the call (BR-010), performs the seam
read, and returns names. The browser never holds a credential and never talks to a vendor.
*Alternative rejected:* the frontend hitting the vendor API directly — it would put a PAT in the
browser, which BR-010 exists to forbid.

**D3 — degradation is a successful response with a stated reason, not a 500.** "Directory absent",
"listing refused by the vendor", and "no Connector" are ordinary outcomes the form must render as
today's textbox plus the reason. The endpoint models them as data (empty list + reason), because a
picker that throws teaches the form to treat discovery as load-bearing — the opposite of D4.

**D4 — the suggestion is a convenience; the save path does not change.** No validator learns about
the listing. A name not in the list saves exactly as today and fails at Run time exactly as #150
specified. This mirrors the output-label picker's rule: every refusal lives where the Automation
is saved, so a caller bypassing the portal is treated identically.

**D5 — the picker lists `.md` entries only, non-recursively, names relative to the prompts
directory.** Subdirectories are out: the Automation stores a name resolved against the directory,
and one level is what the starters convention seeds. A team nesting prompts can still type the
relative path by hand (free text).

## Risks / Trade-offs

- [Vendor rate limits on a chatty form] → the query hook fetches on field focus (not per
  keystroke) and TanStack Query de-duplicates; one listing per form open in the common case.
- [AzDO path is unexercised] → same posture as every other AzDO seam method (ADR-0005): translation
  unit-tested, labelled hypothesis in code and in the vendor picker; first real connection is a
  test, not a deployment.
- [Large prompt directories] → names only, one directory level, no content reads; the response is
  bounded by the team's own file count.
- [Default branch drift] → the listing names the branch it read implicitly (default at call time),
  same as the Run path; a prompt merged seconds ago appears on the next focus. Accepted: this is
  the same freshness contract every live read in the product already has.

## Migration Plan

Additive: one seam method (both vendors), one query use case, one form change. No schema change,
no data migration, no config change. Rollback is reverting the change; nothing persists.

## Open Questions

(none — the shape questions were settled at grill time on #215)
