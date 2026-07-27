# Design — local-agent-loop

## D1 — The worker is a host, so the AppHost must treat it as one

Since #18 the worker composes the modules: it needs the database, the queue, and the
configuration the secret resolver reads. The AppHost still wired it as the #16 message-drainer.
This is the fix, and it is the reason the resource has never actually run — an explicit-start
resource nobody starts is a resource nobody tests.

## D2 — Restart-on-exit, with the KEDA divergence stated

The worker drains and exits by design (#16 D3). Locally that means a queued Run waits until
something starts it again, so the AppHost restarts it. This is **not** KEDA: KEDA scales on
queue length and can scale to zero, Aspire restarts unconditionally and burns a little idle CPU.
Stated in the spec, in the AppHost comment, and in the docs — the alternative is a developer
concluding the scale rule works because the loop does.

## D3 — The seeder exists only in run composition, structurally

It is registered by the AppHost's run-mode branch passing a configuration flag the Server reads;
no deployed template sets it, and the seeder refuses to run without it. "Structurally impossible
to reach" beats "we would never set it in production" — the second is a promise, the first is a
property.

## D4 — The seeder is idempotent and names what it points at

It creates a project, a Connector for a repository named in configuration, and an OpenCode
Automation — once. Re-running finds them and does nothing, because a data volume survives
restarts and a seeder that duplicates on every boot is worse than none. It never invents a
repository: the developer supplies one they control, or the seeder skips the Connector and says
so.

## D5 — The credential story locally is configuration, not a vault

With no `Secrets:KeyVaultUri`, the resolver already reads secrets from configuration
(`ConfigurationSecretResolver`, #24). The developer puts their PAT in user secrets under the
name the seeded Connector uses. No token enters the repository, and the docs state the one
command. The AI credential is not needed at all — the seeded Automation uses opencode's free
model (DEC-044).
