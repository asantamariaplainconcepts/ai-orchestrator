# Tasks — output-label-set

## The contract

- [ ] 1.1 `Automation.OutputLabel` becomes `OutputLabels`, a set, deduped the way the vendor compares
      labels (design D4).
- [ ] 1.2 `AutomationDetail.OutputLabel` follows in Contracts; its one consumer is `RunExecutor`.
- [ ] 1.3 A migration that **rewrites** the column into `text[]`, preserving every configured value —
      hand-written, because EF's generated type change is drop-and-add (design D1).

## Configuring

- [ ] 2.1 Create and update accept a set, each member bounded as the single label is today.
- [ ] 2.2 #115's self-trigger refusal applies to every member, named in the refusal.
- [ ] 2.3 The canvas's connect adds a label to the set; disconnect removes that one and leaves the
      rest.

## Applying

- [ ] 3.1 `HandOn` applies every label through the ordinary write path; the grill's default stays the
      default of an empty set (design D6).
- [ ] 3.2 Every label is attempted, and the Run fails naming every one that did not land (design D2).

## The surfaces

- [ ] 4.1 The output labels input is a picker that also accepts free text, suggesting other **enabled**
      Automations' triggers and never this Automation's own (design D5).
- [ ] 4.2 The canvas draws one edge per matching label, and says branches serialize rather than run at
      once (design D3).
- [ ] 4.3 The board's ordering reads the set rather than the single label.

## Verification

- [ ] 5.1 Functional: several labels leave together; one refused label does not hide the others and the
      Run fails naming them; a failed Run applies nothing.
- [ ] 5.2 Functional: the same label in two spellings is stored once; a member equal to the trigger is
      refused.
- [ ] 5.3 Migration: an Automation configured with a single label before the change behaves identically
      after it — exercised against a real database, not asserted.
- [ ] 5.4 E2E: two edges leave one node, and disconnecting one keeps the other.
- [ ] 6.1 CI green; evidence on #165.
