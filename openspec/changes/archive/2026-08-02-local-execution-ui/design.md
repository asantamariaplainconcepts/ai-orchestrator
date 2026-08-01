## Context

#210's backend is live and invisible. The five surfaces were elaborated as design mocks 3a–3e
(design project: `local-code-source.md`, `run-execution-locus.md`, `local-onboarding.md`); the
issue's acceptance criteria pin the interaction details (target sizes, aria behaviour, copy
through the catalogue). Everything here is frontend consuming landed endpoints; the one unlanded
contract is #212's set-up-defaults action, already specification-validated and in code-review.

## Goals / Non-Goals

**Goals:**
- The self-host flavour is visible and safe: configuration validates live, choices state their
  consequences, every Run says where it executed.
- Cloud deployments render exactly nothing of this — no disabled stubs, no hidden tabs.
- A fresh self-host owner reaches a closed loop guided by state derived from live data only.

**Non-Goals:**
- A Browse/folder-picker dialog (typed path + recent folders only).
- Auto-creating default Automations (that is #212's own surface; the checklist only links it).
- Any new backend endpoint or any change to #210's contracts.
- Storing onboarding progress anywhere.

## Decisions

**D1 — probe once, render nothing on cloud.** The settings slice asks the code-source surface
once per project page load; a 404 means the segmented control, the callout and the recent folders
never mount. *Alternative rejected:* rendering a disabled control with an explanation — a control
that can never be enabled is furniture, and the spec's posture rule (404 = the option does not
exist) says the UI must match it.

**D2 — the Run now dialog exists only when there is a choice.** No local folder → today's
immediate dispatch, no dialog. With a folder, radio cards state consequences and the disabled pod
card carries its reason (#210: a LocalFolder project cannot run in a pod). The primary button
repeats the selection ("Run on this machine" / "Run in a pod") so the click restates the decision.
Refusals (BR-001 conflict, BR-013 rules, dirty tree) render inside the dialog, aria-live polite,
naming the folder — the gesture's own surface, per the issue's bar.

**D3 — locus is a chip vocabulary, used twice.** One chip component (monitor glyph + word for
Local), on the Run detail beside the state pill and as the quiet outline badge on the projects
list — severity/locus never colour alone (icon + text always).

**D4 — the checklist is derived, never stored.** Three steps computed live: Connector configured →
Automations exist → a Run reached terminal. Any terminal Run anywhere in the project retires the
checklist permanently (computed from the runs read model, not a flag). Step 3's action button
invokes #212's endpoint; until #212 merges, the step renders as guidance without the one-click
action, and the wiring task lands behind the merge.

**D5 — the banner keys on the principal, not the posture.** `role="status"`, warning family,
every screen, present exactly while the principal is the `local-owner` sentinel — a signed-in
principal removes it. It states what the sentinel means (this portal trusts the machine's owner).

## Risks / Trade-offs

- [#212 slips and step 3 has no action] → the checklist still guides (D4 renders guidance);
  the wiring task is isolated and lands separately.
- [Probe adds a request per project page] → one GET, cached per page load by the query layer;
  cloud answers 404 once and the slice never asks again within the page lifetime.
- [Recent folders may name paths that no longer validate] → selection re-runs the live validation
  exactly as typing does; a stale entry fails with the named check, never saves silently.

## Migration Plan

Frontend-only; ships dark until the surfaces render (cloud renders nothing by construction).
Rollback is reverting the change.

## Open Questions

(none — the mocks and the issue pin the interaction bar)
