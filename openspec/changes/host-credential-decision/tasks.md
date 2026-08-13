## 1. Establish the record the decision is made against

- [ ] 1.1 Confirm the seam's actual shape by reading it, not by assuming: how many `IBacklogConnector`
      methods take a credential, and how each implementation turns it into a client — recorded in
      `evidence.md` §3
- [ ] 1.2 Quote, rather than paraphrase, the two existing delegations the issue cites as precedent —
      `AgentCredentialEnvironment.For`'s shadowing rule and `connector-configuration`'s local-folder
      carve-out — and state precisely what each one delegates, so the ADR argues against the real
      precedent rather than a summary of it
- [ ] 1.3 Exercise option (d)'s open question for real rather than asserting it: whether a git
      credential helper's output can tell a product what the credential may do — recorded in
      `evidence.md` §1 with the command and its output
- [ ] 1.4 Verify ADR and DEC numbering against current `origin/main`, and check the open branches for
      a colliding ADR — recorded in `evidence.md` §2

## 2. Evaluate the four options against one set of criteria

- [ ] 2.1 Judge **(a) status quo** against every criterion in design D1, stating what it costs a
      self-host owner concretely (a minted PAT) rather than abstractly
- [ ] 2.2 Judge **(b) delegate reads to the host's `gh` CLI**, and state what happens on Azure DevOps —
      naming whether the asymmetry is fatal or merely awkward
- [ ] 2.3 Judge **(c) reads delegate, writes name a credential**, and state that #347 covers writes,
      so this option is eliminated **by construction** — as a finding the ADR records, not an
      inference left to the reader
- [ ] 2.4 Judge **(d) delegate to the machine's git credential helper**, answering the question the
      issue set it: whether a helper's output may authenticate vendor **API** calls and not only git
      transport — using §1.3's finding, and distinguishing *can it work* from *can the product know
      it will*
- [ ] 2.5 Answer criterion 5 for the winning option: what a Run's record says about which identity
      touched the vendor, naming `IAgentProcessHost.CredentialSource` rather than inventing a
      mechanism
- [ ] 2.6 Record the rejected options with their reasons, so the next person with this idea reads the
      analysis instead of relitigating it

## 3. Write the decision

- [ ] 3.1 Write `docs/adr/0028-<slug>.md` following the repository template — context, decision,
      consequences (positive, negative, neutral), alternatives considered, references — citing the
      measurements from group 1 as its evidence, per the `decision-records` requirement that an ADR
      names its evidence and its check
- [ ] 3.2 State the habitat answer as its own paragraph — one rule or two — so it cannot be read out
      of the ADR by inference
- [ ] 3.3 State the consequences a permissive outcome inherits whether or not it is chosen: two
      authentication modes in the seam permanently, a credential that can expire mid-Run, and a
      resolution that must never block a polling cycle
- [ ] 3.4 State plainly, in the ADR itself, that it was decided unattended with no human reading it
      before it merged (DEC-068 / ADR-0027), and that it is supersedable on that ground

## 4. Record it where the product's decisions live

- [ ] 4.1 Add the OPN-006 entry to `docs/product/mvp/07-open-decisions.md`, naming what it blocked —
      hiding the credential in self-host, and any host-derived vendor authentication — and close it in
      the same edit with a pointer to ADR-0028, following the OPN-002/005/007 convention
- [ ] 4.2 Remove the *"Still open: OPN-006"* paragraph and add OPN-006 to the file's **Closed:** list,
      so the count stays honest
- [ ] 4.3 Add **DEC-069** to `docs/product/mvp/10-locked-mvp-decisions.md` following that file's
      shape — decision, rationale, costs accepted and stated, date and issue — appended, never
      editing a locked entry in place

## 5. Write the spec delta the decision produced

- [ ] 5.1 Replace the outcome-independent text in `specs/connector-configuration/spec.md` with the
      answer that was chosen, in both modified requirements
- [ ] 5.2 Replace the outcome-independent text in `specs/connector-seam/spec.md` the same way
- [ ] 5.3 Run `openspec validate --change host-credential-decision --strict` and fix what it reports

## 6. Verify and hand off

- [ ] 6.1 Run the repository's CI-equivalent gates for a docs-and-specs change — the pre-commit hook
      (CSharpier + lint-staged Prettier/ESLint) must pass on the actual commit, not be skipped
- [ ] 6.2 Confirm no code changed: `git diff --stat` touches only `docs/`, `openspec/`, and nothing
      under `src/`
- [ ] 6.3 State on #347 what this decision unblocked or refused, so the blocked issue's next step is
      written where its owner will read it — never leaving it to infer the outcome from a merged PR
- [ ] 6.4 Capture the retro material for `/aio:sync` to append — `retro-entry` owns
      `docs/process/retro-log.md` and runs at sync, so appending here would duplicate it
