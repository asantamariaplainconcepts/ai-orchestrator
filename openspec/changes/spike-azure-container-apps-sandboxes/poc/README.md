# Spike harness — ACA Sandboxes

Run in order. Everything here is **checks and local build only** until step 2; nothing creates an
Azure resource without you typing the command yourself.

Two conventions, both deliberate:

- Commands marked **reported** come from Microsoft's own CLI reference, the portal docs, or a
  third-party write-up, and have **not** been exercised. The preview surface moves — re-read
  `--help` before trusting any flag here (its own docs say so).
- Commands marked **verified** were run on the date given, and the date is part of the claim
  (ADR-0018).

## 0. Preflight — verified 2026-08-08

```bash
bash 00-preflight.sh
```

What it found on the authoring machine that day: Azure CLI 2.82.0, signed in to a Visual Studio
Enterprise subscription, `Microsoft.App` **Registered**, and — checked separately —
`Microsoft.App/sandboxGroups` present at api-version `2026-02-01-preview`, offered in **Spain
Central** among others. The only gap was the `aca` CLI, which is not installed.

So the blocker was never the subscription.

## 1. The image — verified 2026-08-08

```bash
docker build -t aio-spike-aca:local .
docker run --rm aio-spike-aca:local sh -c 'node --version; git --version; opencode --version'
```

Observed: node v22.23.2, git 2.39.5, opencode 1.18.6, workdir `/workspace`, 256 MB. The packaging
half of H1 is done; what remains is whether the platform's disk-image import accepts it.

## 2. Group, credential, sandbox — reported

```bash
aca sandboxgroup create --name <GROUP> --location spaincentral --set-config
aca sandboxgroup credential create --group <GROUP> --type github-copilot   # prompts for the token
aca sandbox create --group <GROUP> --disk copilot --credential <ID> \
  --egress-default Deny --egress-rule "github.com:Allow"
```

Start with `--disk copilot` — the public prebuilt image — so a failure here is about access rather
than about our packaging. Only then import the image from step 1 and use `--disk-id`.

The credential type list reported for this preview is `github-copilot` and `anthropic-claude`, and
the Copilot one is reported to reject classic `ghp_…` tokens in favour of fine-grained
`github_pat_…`.

## 3. The data plane — reported

These are the verbs that decide whether this substrate fits `IAgentProcessHost`:

```bash
aca sandbox exec     --id <SBX> -c "opencode run -m <model> '<prompt>'"
aca sandbox fs write --id <SBX> --path /workspace/x --file ./x
aca sandbox port add --id <SBX> --port 5173            # omit --anonymous to keep it Entra-gated
aca sandbox egress   set --id <SBX> --default Deny --rule "github.com:Allow"
```

One per interface member: the command and its streamed output, the workspace, the preview port,
the credential boundary.

## 4. Record

Fill in `../findings.md` as you go — per hypothesis, with the command and its actual output.
Anything not exercised stays **not verified**; that is the spec's rule for a spike and the reason
this file distinguishes reported from verified in the first place.
