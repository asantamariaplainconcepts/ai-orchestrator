## 1. Establish the record the decision is made against

- [x] 1.1 Re-read ADR-0008 and confirm, in notes, exactly which of its three pillars each later
      decision moved — DEC-013's supersession (#296), DEC-061 and DEC-063 (#198) — quoting the
      superseding text rather than paraphrasing it
- [x] 1.2 Verify the two facts the decision rests on that ADR-0008 could not weigh: that a self-host
      sbx sandbox has no per-hour cost, and that a deployed ACA session pool holds a ready instance
      continuously (DEC-063) — the second is a standing cost and must be stated as a number, not a
      shape
- [x] 1.3 Copy the spike's harness and findings into `poc/` and confirm the feasibility claims in the
      proposal are the ones the spike actually measured — pty allocation, signals, geometry,
      full-screen rendering — and nothing beyond them
- [x] 1.4 Confirm the transcript premise in design D3 against the code as it stands after #299/#300:
      that both runtimes are invoked headless with structured output and the Output surface renders
      from that stream

## 2. Evaluate the three candidate shapes

- [ ] 2.1 Judge shape 1 (reaffirm: a pass per message) against every criterion in design D2, and
      record what it costs the Member in latency and passes for a dozen-round grill
- [ ] 2.2 Judge shape 2 (an attached session bounded by inactivity) against the same criteria,
      including what a held sandbox costs in each habitat and what reclaims it
- [ ] 2.3 Judge shape 3 (split by habitat) against the same criteria, and state whether one product
      behaving differently on two substrates is acceptable for this capability
- [ ] 2.4 Answer design D3's distinction explicitly for each shape: *attach to the agent's own
      process* versus *attach to the agent's sandbox beside it* — including which one preserves the
      structured transcript
- [ ] 2.5 Record the rejected shapes with their reasons, so the analysis is inspectable and the next
      person with this idea reads it instead of relitigating it

## 3. Write the decision

- [ ] 3.1 Allocate the ADR number against `docs/adr/` on current `origin/main`
- [ ] 3.2 Write the ADR following the repository template: context, decision, consequences (positive,
      negative, neutral), alternatives considered, references — citing the specific incidents and
      measurements from group 1 as its evidence
- [ ] 3.3 Mark ADR-0008 superseded in the new ADR if the conclusion differs, leaving ADR-0008's text
      intact; if the conclusion reaffirms it, state that explicitly and say which pillar now carries
      the weight
- [ ] 3.4 Name the consequences a permissive outcome inherits, whether or not it is chosen: the
      held-sandbox reaper, the second writer in the agent's workspace, and the authorization and audit
      gap for an attached session
- [ ] 3.5 State the habitat answer — one rule or two — as its own paragraph, so it cannot be read out
      of the ADR by inference

## 4. Record it where the product's decisions live

- [ ] 4.1 Add the OPN-007 entry to `docs/product/mvp/07-open-decisions.md`, naming what it blocked,
      and close it in the same edit with a pointer to the new ADR
- [ ] 4.2 Fix the file's stale summary: it asserts "None remain open" while #223 is open as
      *Close OPN-006*
- [ ] 4.3 Add the `DEC-*` entry to `docs/product/mvp/10-locked-mvp-decisions.md` if the decision
      changes or revises a locked one, following the "revises X" convention already in that file
- [ ] 4.4 Update `ARCHITECTURE.md` only if the runtime seam's stated shape changed

## 5. Write the spec delta the decision produced

- [ ] 5.1 Fill in the chosen shape in `specs/run-orchestration/spec.md` — replacing the
      outcome-independent "exactly one of two shapes SHALL be named" with the one that was named
- [ ] 5.2 Fill in `specs/agent-sandboxing/spec.md` the same way: whether a human extends a sandbox's
      life, and if so what reclaims it
- [ ] 5.3 Run `openspec validate --change close-opn-007-live-agent-session --strict` and fix what it
      reports

## 6. Hand off what this change deliberately did not do

- [ ] 6.1 Open the follow-on capability issue citing the ADR, sequenced behind #245 (which also
      touches the `Automation` aggregate), scoped to whichever shape the decision permitted
- [ ] 6.2 Open an issue for `ConversationGate.AskAndWait` having no production caller — the grill
      never actually asks, and that is true whichever way this decision went
- [ ] 6.3 Open an issue for the portal answer box (ADR-0008's own named follow-up), noting its
      prerequisite `AddComment` has landed on both connectors
- [ ] 6.4 Append the retro entry, including the correction made during the grill: an apparent
      "Enter key does nothing" pty defect that was the test harness, not the product
