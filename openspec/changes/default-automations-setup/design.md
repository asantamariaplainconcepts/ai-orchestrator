# Default automations setup — design

## Context

Automations belong to the Projects module (`Features/Automations`); the starter catalogue (#190)
is embedded content read by `StarterCatalogue` from a manifest, deliberately never written
anywhere. BR-003 refuses overlapping triggers with a unique index over normalised label + state.
The design-review checklist (mock 3d, issue #211) needs one button that makes a fresh project
runnable.

## Goals / Non-Goals

**Goals:** one idempotent, conflict-proof action that creates the portable tier's Automations
wired as a pipeline, and tells the Admin exactly what it did and what is still missing.

**Non-Goals:** writing prompt files to the repository; changing the starter prompts; a UI (the
checklist button is #211); wiring the workflow tier (it assumes a methodology a fresh project
has not adopted).

## Decisions

- **D1 — the wiring is manifest content, not code.** Each portable-tier prompt gains an optional
  `automation` block (`trigger`, `requiresApproval`, `outputLabels`). The catalogue already keeps
  prompts as content for #190's portability promise; the wiring is the same kind of fact, and a
  team that forks the catalogue changes one JSON file. The existing manifest-enumeration test
  extends to refuse a wiring that names an unknown prompt or duplicates a trigger.
- **D2 — skip, never collide.** The action loads the project's existing Automations first and
  creates only the wired triggers not already present (compared the way BR-003 compares —
  case-insensitive). A concurrent save losing the race to the unique index is answered by
  re-reading and reporting that trigger as skipped: the action's promise is convergence, not
  insertion.
- **D3 — missing prompts are reported, not fixed.** For each created Automation the action reads
  its prompt through the same seam a Run would (`IDocumentReader.ReadPrompt`) and lists the paths
  that failed — with where they belong — because #190 decided the product never writes to the
  repository, and this action inherits that decision.
- **D4 — Automations are created enabled.** A disabled starter set would look like the action
  failed; BR-003's overlap protection already guards enablement, and D2 means nothing conflicting
  was created.

## Risks / Trade-offs

- [The catalogue's wiring encodes a methodology] → it lives in the manifest as content, portable
  tier only, and names only its own prompts; adopters edit JSON, not code.
- [Reading N prompts costs N vendor reads] → N is five and the caller is a human pressing a
  setup button once per project.

## Migration Plan

None — no schema change.

## Open Questions

None.
