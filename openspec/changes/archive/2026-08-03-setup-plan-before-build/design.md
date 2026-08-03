# Design: setup-plan-before-build

## D1 — The plan belongs in discovery, not in a new endpoint

Discovery already reads every candidate directory's file list, and the canonical steps are a
compile-time property of the embedded catalogue. Everything a plan row needs is therefore already in
hand at the moment discovery answers.

A separate "preview" endpoint would have re-read the same listings to compute the same rows, and
introduced a second place where "what the build will do" is decided — which is the bug this change
exists to fix, one level up.

## D2 — A step nothing will happen for is listed, not dropped

Each row carries `Installable`. A step whose tier requires something this project may not have can
be *wired* to a file that exists, but no starter is ever written for it. Rows where neither is true
are filtered out — there is genuinely nothing to say — but the distinction is carried rather than
collapsed, because "this step exists and will not be set up" is an answer somebody may need.

## D3 — Removing the checkbox is a decision, not tidying

#229 added it as a second consent: creating Automations and writing files into a repository are
different acts. That reasoning was sound while the writing was invisible.

It is not, now. The rows say which files would be installed, by name, before anything is pressed. A
checkbox on top of that asks somebody to confirm what they have just read — and a consent that
restates the preview trains people to click past both. The consent moved into the plan; the safety
property moved next to the button, where the draft-pull-request sentence says what makes this
reversible.

## D4 — What the E2E can and cannot reach

The plan renders only once discovery has succeeded, which requires a Connector serving directory
listings. This tier's GitHub stub answers issues only, so that state is unreachable without
extending it — its own change.

The E2E therefore asserts what it can honestly reach: the checkbox is gone. The plan's content is
asserted in the functional suite, against the API that computes it, where a listing can be arranged.
Saying so is the point; a test driven to a state the tier cannot produce would be a fiction.
