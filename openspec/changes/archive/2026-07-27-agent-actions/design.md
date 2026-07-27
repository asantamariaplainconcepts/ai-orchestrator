# Design — agent-actions

## D1 — Dispatch on the action, one shape per action

`RunExecutor` currently gates: anything but `ImplementToPullRequest` fails. It becomes a switch
over four actions, each contributing a prompt and a way to consume the answer. The workspace
ceremony stays exclusive to the PR action — the other three touch no code and must not clone,
which is also why they are fast and cheap.

## D2 — The Agent's answer is the payload, and it is text

Comment, transition and estimate all take the runtime's log as the Agent's answer. No structured
output contract is introduced: the runtimes' shapes already differ (#30), and inventing a
cross-runtime JSON schema for "one short answer" would be a second contract to keep aligned. For
the estimate the number is parsed from the answer's first integer, and a non-numeric answer is a
stated failure rather than a guessed zero.

## D3 — The estimate is a label because that is what GitHub has

Owner decision. `estimate:<n>` is written with the label writes that already exist (#24), and
any prior `estimate:*` is removed first so a Story never carries two. A Projects v2 custom field
would need a board to exist, a second API surface, and per-project configuration of which field
to write; a comment alone would leave nothing sortable. The reasoning still goes in a comment —
UC-019 asks for both.

## D4 — Transition validates rather than trusts

The Agent proposes a target state as text. GitHub accepts only `open`/`closed`, so the
implementation maps and refuses anything else with a stated reason. When the Automation gains a
configured target (its own issue) this becomes a lookup instead of a parse — the seam does not
change.

## D5 — Every action is idempotent-ish, and where it is not, it says so

Comments are additive by nature: a re-run comments twice, and BR-004 means only a human causes a
re-run, so that is acceptable and stated. The estimate label is replace-then-set, so a re-run
converges. A state transition to the state already held is a no-op at the vendor.
