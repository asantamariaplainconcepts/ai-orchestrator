# ADR-0018: a measurement licenses only what it measured

- **Status:** Accepted
- **Date:** 2026-08-08
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** process, verification

## Context

[ADR-0001](0001-verify-by-exercising.md) requires claims to be exercised rather than reasoned
about, and the practice works: this programme's last several changes each found something real by
running the thing. But two changes in a row then failed the same *second* way — the measurement
was genuine, and the claim written from it was wider than the measurement.

**First occurrence, `sandbox-carries-the-owners-session` (#288).** The change's design opened by
observing, by hand, exactly which credential files a CLI needs inside a sandbox. That observation
was real and it set the scope correctly. It was performed by the machine's owner, copying files as
themselves. The shipped code has the *server* copy those files *on the owner's behalf* — and
`sbx cp` preserves the host's uid and mode, so a 0600 file owned by uid 501 landed unreadable to
the sandbox user. The CLI reported "0 credentials" from a file that was demonstrably present. The
retro stated the rule at the time: an observation holds for the principal that made it.

**Second occurrence, `automation-and-run-choose-the-model` (#291).** The design's D5 said "both
CLIs reject an unknown model cleanly and **name the model in the error**". One had been measured:
`claude --model definitely-not-a-model` answers `404 … "model: definitely-not-a-model"`. The other
had not. When it was finally run, `opencode run -m definitely/not-a-model` answered `UnknownError`,
*"Unexpected server error. Check server logs for details."* and an opaque ref — the model named
nowhere. Passing that through would have reported a typo as somebody else's outage.

Different surfaces, one shape: **the measurement was of one instance, and the claim was about the
class.** Neither was caught by review, because a design citing a real observation reads as
rigorous — the missing half is invisible unless somebody asks "which one did you actually run?".

## Decision

A claim written from a measurement SHALL be no wider than the measurement, and where it is wider,
the gap SHALL be named.

Concretely, a design or spec statement that quantifies over a set — *both* runtimes, *every*
habitat, *any* CLI — SHALL either cite a measurement for each member, or say plainly which member
was measured and that the rest is inference. "Observed on X; assumed to hold for Y" is an
acceptable and useful sentence. "Observed" with a quantifier in front of it, when only one member
was run, is not.

Two members of a set is the common case here and the cheap one: the second measurement usually
costs one command.

## Consequences

- **Positive:** the failure mode both changes hit becomes visible at design time, when it is a
  sentence, rather than at implementation time, when it is a defect that has already shaped code.
- **Positive:** it gives a reviewer a concrete question to ask of any design citing observation —
  which member was run — instead of having to take rigour on trust.
- **Negative:** more measurements, and more hedged sentences in designs. Some of the hedges will
  turn out to have been unnecessary.
- **Neutral:** it narrows ADR-0001 rather than replacing it. Exercising remains the requirement;
  this says what an exercise entitles you to write.

## Alternatives considered

- **Leave it to review.** Rejected: review is exactly what missed it twice. A design that names a
  real observation is persuasive precisely when its scope is wrong.
- **Require every member of every set to be measured.** Rejected as too strong — some sets are
  open (every future runtime), and a rule nobody can satisfy gets ignored rather than followed.
  Naming the gap is achievable and is most of the value.
