# Design: local-owner-identity

## D1 — A principal, always; the habitat only decides which one

The seam answers "who is asking", never "is anyone asking". Callers receive a principal with an
id, a display name and a role, and there is no null case and no `IsAuthenticated` flag for code
to branch on — because the moment such a flag exists, every call site acquires a second path, and
in a hosted deployment it is the path nobody exercises.

That shape is chosen for the implementation that does not exist yet. When Entra arrives it
replaces one implementation of this seam and touches nothing above it; if the seam had been
"local mode on/off", Entra would have had to unpick every branch.

**BR-009 stays exactly as written and stays unimplemented.** No operation gains a permission
check in this change: the rule says every operation names a required permission, and doing that
across the product is its own slice. What changes is that when those checks are written, they
will have somebody to check against — today they would have nobody, which is why they are not
written yet.

## D2 — Two locks, because either alone fails a different way

Configuration discipline (Terraform never sets the value) does not survive a person editing the
Azure portal. A silent ignore in Production leaves that person believing they enabled something.
So: Terraform omits it *and* the server refuses to start when the local mode meets a Production
environment or a non-loopback public URL, saying which of the two it found.

Refusing to start, rather than warning: the failure mode being prevented is a deployment that
serves an implicit Admin to the internet, and a process that will not start is the only signal
nobody can miss. The precedent is the dispatch worker that refuses to start without a database
connection (#92) — the same argument, a worse consequence.

## D3 — The temporary state gets a voice

Today's Azure deployment authenticates nobody and says nothing about it. Once identity exists as
a concept, a hosted start with neither local mode nor a provider is a *third* state — real,
temporary, and until now invisible. It logs a warning naming OPN-002 at startup.

This is not decoration. A condition with no voice is how a stopgap becomes permanent, and the
retro log has already recorded that shape once (#86's lesson about notes nobody tracks).

## D4 — The local owner is a fact about the deployment, not a stored user

No users table, no seeding, no row to migrate: the local owner is constructed from configuration
at startup, the same way the framework's conventions are code rather than data. Runs and comments
attribute to it by name, so the audit trail says "Local owner" instead of nothing — honest about
what it knows, which is that one person has the machine.
