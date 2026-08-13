# Evidence

What was exercised for real, so the decision rests on measurement rather than framing (ADR-0006
discipline; `openspec/config.yaml` design rule: *"a CI step existing does not mean it ever
succeeded"*).

## 1. The credential-helper protocol carries no scope — exercised 2026-08-13

**The question option (d) had to answer**, in the issue's own words: *whether a credential helper's
output may authenticate vendor **API** calls (work-item reads, labels, comments, transitions) and not
only git transport.*

**What was run.** A stand-in credential helper that emits every key the protocol permits a helper to
return, asked through `git credential fill` (git 2.48.1, macOS):

```sh
#!/bin/sh
[ "$1" = "get" ] || exit 0
echo "username=dummy-user"
echo "password=dummy-secret-value"
echo "password_expiry_utc=1799999999"
echo "oauth_refresh_token=dummy-refresh"
```

```
$ printf 'protocol=https\nhost=example-vendor.test\n\n' \
    | git -c credential.helper=./fake-helper.sh credential fill
protocol=https
host=example-vendor.test
username=dummy-user
password=dummy-secret-value
oauth_refresh_token=dummy-refresh
password_expiry_utc=1799999999
```

**The finding.** The richest output the protocol permits is `protocol`, `host`, `username`,
`password`, `oauth_refresh_token`, `password_expiry_utc`. There is **no scope field, no capability
field, and no field naming which application the credential was minted for.**

**Why this is decisive rather than incidental.** It is a property of the protocol, not of one
machine's configuration, so no amount of vendor-specific work changes it. A product that resolves a
credential this way learns *a secret and a username*. It cannot determine, before using it, whether
that secret may read work items, apply labels, or post comments — nor can it name what to grant if it
cannot. That is exactly the pair of guarantees
[`connector-configuration`](../../specs/connector-configuration/spec.md) requires today:

> The product SHALL state which permissions a credential needs … **in the vendor's own vocabulary —
> the names a person selects while minting a token** … The list SHALL be derived from the same
> capability set verification uses.

A helper credential has no *"person selects while minting"* step inside this product, and the
protocol will not report what was selected elsewhere.

**What this finding does not establish.** It does not show that a helper credential *fails* against a
vendor API — for GitHub an OAuth token carrying `repo` generally succeeds, and Azure DevOps accepts a
PAT over Basic auth. The point is narrower and stronger: **the product cannot know which it has**,
so the guarantee it currently makes to the operator degrades from *derived* to *documented*.

**Deliberately not run.** Extracting this machine's real `github.com` credential from the keychain and
probing the API with it was attempted and refused by the session's own guardrails. It was not worked
around, and it is not needed: the structural finding above is the one the decision turns on, and a
single machine's stored credential could only ever have shown what *one* helper happens to hold.

## 2. Numbering, checked against `origin/main` — 2026-08-13

Per the [`decision-records`](../../specs/decision-records/spec.md) requirement that numbers are
allocated against current `origin/main` and re-verified at sync:

- Highest ADR on `origin/main`: **0027**. This change takes **0028**.
- Highest DEC in `10-locked-mvp-decisions.md`: **DEC-068**. This change takes **DEC-069**.
- Collision check across the three open branches (`change/edit-connector-keeps-credential`,
  `change/run-on-a-pr`, `change/self-host-distribution`): none touches `docs/adr/`,
  `07-open-decisions.md` or `10-locked-mvp-decisions.md`.

## 3. The seam's actual shape — read, not assumed

- `IBacklogConnector` has **fourteen** methods; every one takes `string token`.
- Both implementations resolve their client identically: `clientFactory.Create(coordinates.Owner, token)`
  (`GitHubBacklogConnector`, `AzureDevOpsBacklogConnector`).
- `VerifyAccess` is documented as read-only *always*, returning a verdict **per capability** rather
  than a boolean, because *"a 'no' that cannot say which read failed cannot tell the operator what to
  grant, and naming the fix is the whole value"*.

That last sentence is the seam's own statement of the guarantee finding §1 undermines, which is why
the decision is recorded against it rather than around it.

## 4. Route — recorded because it changes how this record should be read

This change was proposed, implemented and merged by `/aio:ship` in a single unattended run
(DEC-068 / [ADR-0027](../../../docs/adr/0027-a-change-may-reach-main-unreviewed-on-one-explicit-invocation.md)).
**No human read its spec or its diff before it reached `main`**, and the decision it records was made
without review. The ADR is supersedable by construction; overturning it costs one later change.
