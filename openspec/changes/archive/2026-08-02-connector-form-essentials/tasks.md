## 1. The disclosure

- [x] 1.1 Load the `aio-design` skill; add the Advanced disclosure to the Connector panel using an
      existing primitive (no new component beyond it)
- [x] 1.2 Derive its open state (design D1): open when the Connector stores a prompts directory, a
      code repository, or a non-default code source
- [x] 1.3 Lock it open while the local-folder code source is selected, stating why it cannot be
      collapsed — the API requires that path

## 2. One credential input

- [x] 2.1 Replace the mode select with a single token input plus a plain control to name a secret
      instead
- [x] 2.2 Swapping discards the replaced value (design D2), so the request can never carry both;
      blank on an edit still means keep-the-stored-one (#160)

## 3. Fields the code source makes inapplicable

- [x] 3.1 Under the local-folder code source, do not render the code repository input and state
      once why
- [x] 3.2 Send `codeRepository: null` in that case — hiding and clearing are one act (design D3)

## 4. Copy and placement

- [x] 4.1 Move each hint beside its field; retire the pooled paragraphs at the form's end
- [x] 4.2 i18n entries for the disclosure, its locked-open reason, the credential mode control and
      the inapplicable-field line — no hardcoded copy

## 5. Proof

- [x] 5.1 Browser-preview verification: a fresh project shows four inputs; an existing Connector
      with advanced values opens disclosed; selecting the local folder locks it open; switching
      code source clears and restores the code repository; the cloud posture still renders no
      code-source UI at all
- [x] 5.2 Both themes, keyboard reachable, and the full gates (build, tests, lint, spec
      validation, design-system validator)
