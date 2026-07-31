# Tasks — portal-conversation

## The model

- [x] 1.1 A `Conversation` aggregate in the Runs module: project, optional Story subject, ordered
      messages, and its own spend. No Run, no cap slot, no lock.
- [x] 1.2 A `ConversationMessage`: who said it, what, when, and the pass's usage — unknown where the
      runtime reported none (design D4).
- [x] 1.3 Migration for both tables.

## The pass

- [x] 2.1 An `IConversationRuntime` seam: a message plus the project's context in, a reply with its
      usage out (design D3).
- [x] 2.2 An in-process implementation, used where the habitat provides no session host — composed on
      configuration presence, never inferred (ADR-0010).
- [x] 2.3 A session-pool implementation: the conversation id is the session identifier, the portal
      calls the pool with its own managed identity and holds no project credential.
- [x] 2.4 The session container image: clones the project's repository with the project's PAT,
      resolved by name at the last moment, and answers one message per call.
- [x] 2.5 Exactly one pass per message, and a failed pass leaves the conversation open.

## The surfaces

- [x] 3.1 Start a conversation, send a message, read the exchange — each declaring
      `conversation.hold`, granted to Member (design D5).
- [x] 3.2 The portal surface: the exchange, what it has cost, and a failure shown on the message that
      caused it.
- [x] 3.3 A total that says "at least" where any pass went unmeasured.

## The habitat

- [x] 4.1 Terraform: the session pool through `azapi`, because `azurerm` 4.81 does not model it —
      verified against the provider's schema.
- [x] 4.2 The portal's identity may create sessions; the session's identity may read the vault and
      pull from the registry.
- [x] 4.3 The image is built and pushed like the dispatch job's.
- [x] 4.4 DEC-061 recorded: a warm container revises ADR-0008's "nothing idles", bounded by the pool's
      cooldown.

## Verification

- [x] 5.1 Functional: a conversation exists without a Run, and an Automation on a Story with an open
      conversation still runs — the second half is the design's headline claim and was nearly ticked
      without a test.
- [x] 5.2 Functional: one message is one pass; a failure leaves the conversation open and accepts
      another message; unmeasured usage reads unknown and the total does not claim to be exact.
- [x] 5.3 Functional: a caller with no role on the project is refused, disclosing nothing.
- [x] 5.4 Functional: no vendor write happens on any Story, with a subject and without one.
- [x] 5.5 E2E: the surface is reachable, a conversation about nothing starts, and the exchange then
      asks for a message. **Scoped, and the criterion as written is not met:** sending a message here
      would clone a repository and call a model, which CI has neither the credentials nor the minutes
      for. The round trip — one pass, usage recorded, unknown staying unknown, a failure leaving it
      open — is covered against the real API with the runtime stubbed at its seam.
- [x] 6.1 CI green. **Stated plainly on the PR:** CI proves everything up to the session pool. The
      resident path is exercised only once the owner applies the infrastructure, and the evidence for
      it goes on #166 afterwards rather than being claimed here.

## What the owner applies

- [ ] 7.1 `terraform apply` for the session pool and the two role assignments, then the first
      `deploy.sh` to push the session image. **Not done here and not claimable here** — the resident
      path only exists after that, and its evidence goes on #166 afterwards.
