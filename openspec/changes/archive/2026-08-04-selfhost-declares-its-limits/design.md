## Context

Three habitats compose this product today and only two can reach a folder on the operator's
machine: the dev loop (Server as a host process) can, ACA never offers it, and compose self-host
*offers* it — `HasCodeSource` follows the LocalOwner identity — while its containerised Server
cannot see it. #222 built the capabilities read precisely so a client learns what can succeed
here; this change adds the fact that read is currently silent about.

## Goals / Non-Goals

**Goals:**
- The compose habitat withholds the Local-locus offer, with the reason, before anything fails.
- The refusal exists server-side too — a Connector saved before the declaration, or a request
  hand-crafted around the portal, is refused by name.

**Non-Goals:**
- Executing Runs in compose (that is `docker-run-pods`, #246).
- Simulating reachability with volume mounts (rejected at grill: fragile, and two processes over
  one working copy is the hazard the refusal exists to prevent).
- Changing what the dev loop or ACA offer.

## Decisions

**D1 — declared, never inferred (ADR-0010).** The habitat's composition sets one configuration
value: the *reason* the Local locus is unavailable (`Habitat:LocalFolderUnavailableReason`). The
generated compose sets it; nothing else does. *Alternative rejected:* sniffing
`DOTNET_RUNNING_IN_CONTAINER` — an inference that would also be wrong for a container the
operator deliberately gave a mount, and ADR-0010 exists because inferred habitat contracts rot.

**D2 — the StoreRemedy pattern, one more fact.** The capabilities response gains
`CanUseLocalFolder` and `LocalFolderReason`, exactly parallel to `CanStoreSecret`/`StoreRemedy`:
the capability is derived from the condition that makes it succeed, and where absent it carries
the sentence that says why. `HasCodeSource` keeps meaning "the self-host surface exists" — a
compose deployment still *is* self-host; what it lacks is one locus.

**D3 — refuse at both doors.** The portal withholds the option (reads capabilities), and the API
refuses too: `ConfigureConnector` rejects a `LocalFolder` code source, and `RunCreator` rejects a
Local-locus resolution, both naming the declared reason. Two doors because the portal's honesty
is a courtesy; the API's is the boundary.

**D4 — the reason is the operator's sentence.** The value is the human-readable reason itself,
not a boolean — one string that the capability read, the save refusal and the Run refusal all
carry verbatim, so the three sites cannot drift apart.

## Risks / Trade-offs

- [A stale generated compose omits the declaration] → the compose-drift CI gate regenerates from
  the AppHost; the declaration lives in the AppHost's publish composition, not hand-edited YAML.
- [An operator who really wants a mount] → they can unset the declaration in their own compose;
  the refusal follows the declaration, not the container. Documented in DEC-049 docs as "on you".
- [A Connector saved as LocalFolder before this lands] → the Run-resolution refusal (D3) catches
  it by name; nothing crashes on a container path.
