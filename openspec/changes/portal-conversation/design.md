# Design: portal-conversation

## D1 — A conversation leaves no trace on the Story

The issue asks the design to pick, because ADR-0008 assumed the opposite. It picks **portal-only**.

Three reasons, and the third is what settles it:

1. **"Not a Run" has to mean something.** A conversation that wrote to the vendor on every message
   would be indistinguishable, on the Story, from an Automation that did — and the Story is where
   BR-014's audit trail lives. Chat is not an audit trail.
2. **The criteria already forbid it for half the feature.** A conversation about nothing must produce
   "no vendor write on any Story". Writing for the Story case and not the other makes one feature into
   two, with the difference invisible from the surface that starts them.
3. **ADR-0008's reason for the comment is spent.** It argued the vendor write kept the auth exposure
   "the same class the board already has", back when the portal authenticated nobody. It authenticates
   now (#12) and permission is per project (#13), so the comment is no longer buying the property it
   was chosen for.

What is lost, stated: somebody reading the Story at the vendor cannot see that a conversation happened.
That is a real gap and the honest place to close it is a deliberate "post this to the Story" action, not
a side effect of typing.

## D2 — One session container per conversation, warm while it lives

The pass needs a home with a cloned workspace and the project's PAT. Three shapes were on the table.

**In the portal process.** Rejected on the issue's own terms: the portal would hold project
credentials and clone repositories, collapsing the job/portal identity separation that DEC-030's
credential boundary rests on.

**A dispatch job per message.** Correct and isolated, and it is what the substrate already does — but an
ACA Job is one-shot, so every message pays a cold start. Ten seconds per reply is the difference
between a conversation and a ticket queue, and the workspace would be cloned again each time.

**A session container per conversation** — chosen. Azure Container Apps **dynamic sessions** are built
for exactly this: a pool declared once, sessions addressed by identifier, each its own container, and
the platform reclaiming them after inactivity. The conversation id is the session identifier, so one
conversation is one container is one project's PAT, and the portal creates nothing in ARM — it calls
the pool's HTTP API with its own managed identity.

**Verified, not assumed:** `azurerm` 4.81 exposes no session-pool resource — checked against the
provider's own schema rather than its documentation — so the stack gains the `azapi` provider for this
one resource. The platform side was checked the same way: `az containerapp sessionpool create` offers
`CustomContainer`, `--lifecycle-type Timed` with a cooldown, a user-assigned identity and a registry
identity, which is precisely the set this needs. Neither fact came from memory.

**This revises ADR-0008's "nothing idles" (DEC-061).** A warm container idles between messages. The
revision is deliberate and bounded by the pool's own cooldown; recording it as a decision is what
stops the contradiction from being discovered later as a bug.

## D3 — The runtime seam, not the session API, is what the module sees

`IConversationRuntime` takes a conversation, a message and the project's context, and returns a reply
with its usage. Behind it: the session pool in a provisioned deployment, and an in-process runtime
where there is no pool — the same composition-on-configuration-presence rule every other seam here
follows, and the reason `aspire run` and the self-host compose keep working with no session pool at
all (ADR-0010).

That seam is also what makes this testable. The pool is infrastructure the owner applies; the module's
behaviour — one pass per message, usage recorded, failure leaving the conversation open — is provable
without it, and is proved against the in-process runtime.

## D4 — Usage is recorded per message, and absent usage is absent

BR-011 already says a Run whose usage the runtime did not report reads unknown rather than zero, and a
conversation's total is a sum of messages. A message with unknown usage makes the **total** unknown-ish
rather than silently smaller, so the surface says "at least $X" when any message is unmeasured — the
alternative is a total that looks precise and is not.

## D5 — The permission is the Member's, and it is declared like every other

`conversation.hold`, granted to Member: ACT-002 is the actor the issue names, and a conversation
neither configures anything nor writes to the vendor. It is declared on the command, enforced by the
pipeline, and scoped to the project — so a Member of one project cannot open a conversation about
another, which is the property #13 exists to give and the reason this surface needs no exposure note
of its own.

## D6 — What is deliberately not built

**A cap on concurrent conversations.** BR-002 counts Runs, and a conversation is not one. Making them
count, or giving them their own cap, is a rule change with its own decision — the issue says so, and
inventing one here would be changing BR-002 by implication.

**Telling anybody a reply arrived.** Out of scope in the issue, and it wants the notification decision
that #34's telemetry work did not need.

**A conversation about anything other than a project or its Story.** The subject is one of those two,
which is what keeps "what context does the agent get" answerable.
