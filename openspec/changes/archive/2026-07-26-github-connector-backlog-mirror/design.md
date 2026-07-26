# Design — github-connector-backlog-mirror

## Verified reality (exercised, not assumed — ADR-0001)

- **`api.github.com` is reachable** from this environment: `GET /rate_limit` returns 200.
- **The core rate limit is 5000 requests/hour** for an authenticated token. At the DEC-028 default
  of one poll per minute per project, a single project costs ~60–120 requests/hour depending on
  pagination — comfortable, but not free, and the reason D6 below uses conditional requests.
- **Octokit 14.0.0 resolves from NuGet.** Verified by package search, not assumed available.
- **Aspire's Key Vault integration exists at our version line (13.4.6), and has no emulator.**
  `Aspire.Hosting.Azure.KeyVault` exposes exactly one method — `AddAzureKeyVault(name)` — which
  provisions a *real* Azure vault; there is no `RunAsEmulator`, unlike Storage. The client package
  `Aspire.Azure.Security.KeyVault` offers `AddAzureKeyVaultClient` (registers `SecretClient`) and
  `AddAzureKeyVaultSecrets` (loads vault secrets into `IConfiguration`). Verified by reading both
  packages' API surfaces, not from memory.
- Registry egress for **container images remains blocked locally** (unchanged since Phase 1), so
  the functional tier keeps using the cached Postgres/Azurite images and CI remains the only place
  E2E runs.

## Decisions

### D1 — `Backlog` is a real module, not a feature slice inside `Projects`

ADR-0002's rule is that a module exists only for an independent lifecycle, an event-driven
boundary, or separate ownership. Backlog qualifies on the first: it runs a **background poller**
on its own schedule, writes at a volume unrelated to project administration, and is coupled to an
external vendor's availability and rate limits. Project administration has none of those
properties.

The corpus already models them apart — BC-001 Project Configuration versus BC-002 Backlog Mirror.

**Rejected: a `Backlog` feature folder inside `Projects`.** It would put a vendor-coupled polling
loop inside the module that owns project identity, and the first genuinely independent lifecycle
in the system would be hidden inside something it has nothing to do with.

**A consequence worth stating plainly:** this is the first time two real modules coexist. MOD001–005
and the ArchTests have only ever been exercised against a deliberately-constructed probe. If the
seam is wrong, this change is where we find out — which is a reason to do it now, while one
feature depends on it, rather than later with five.

### D2 — The Connector belongs to `Backlog`, and there is no `.Contracts` project

The obvious shape — `Projects` owns the Connector, `Backlog` polls it — forces a cross-module read
on **every poll**, which needs a `Projects.Contracts` assembly and permanent coupling between a
background loop and another module's surface.

Invert it instead: **`Backlog` owns both `Connector` and `Story`**, each holding a plain
`ProjectId` (a `Guid`, not a type reference). `Projects` continues to own project identity and
knows nothing about backlogs.

This is ordinary DDD across bounded contexts — reference another aggregate by identity, not by
object — and it means:

- **no `.Contracts` project is created**, honouring the playbook's rule that one exists only when
  another module actually consumes it;
- **no cross-module assembly reference exists at all**, so MOD002 has nothing to catch here — the
  boundary is respected by construction rather than by enforcement.

**The cost, accepted:** referential integrity between Project and Connector is not enforced by a
foreign key across schemas. A Connector can outlive its Project. Deleting a Project is not in this
slice; when it arrives it must clean up its Backlog rows, and that will need a domain event rather
than a cascade. Recorded here so it is a known debt rather than a surprise.

### D3 — Secrets resolve per read, through Aspire's `SecretClient`, wired in the host

Aspire's Key Vault integration shapes this, and two of its properties are decisive.

**It has no emulator.** Unlike Storage/Azurite, `AddAzureKeyVault` provisions a real Azure vault
and needs a subscription. So Aspire does **not** remove the need for a development resolver — the
`ISecretResolver` seam stands, and the local implementation over user-secrets stands with it. What
Aspire changes is how the *production* implementation is built: over the `SecretClient` its client
integration registers, rather than one we construct by hand.

**Wiring lives in the host, because a module cannot do it.** `IModule.Add` receives
`IServiceCollection` and `IConfiguration` — deliberately, so modules stay host-agnostic. Aspire's
client integrations are `IHostApplicationBuilder` extensions, which a module therefore *cannot*
call. `Program.cs` is the only place with the builder, so it calls `AddAzureKeyVaultClient` and
registers the resolver; the Backlog module depends on `ISecretResolver` and knows nothing about
Azure. This is a constraint the architecture already implies — worth naming before someone tries
to call an Aspire client extension from inside a module and finds it does not compile.

**Rejected: `AddAzureKeyVaultSecrets`, which loads the vault into `IConfiguration`.** It is the
more convenient option and it is wrong here. Our secrets are **per project, created while the
application is running** — an Admin configures a new Connector and names a new secret. Configuration
is loaded at startup, so a secret added afterwards would not resolve until a restart, and the
failure would look like "the credential is missing" rather than "the process is stale". A per-read
`SecretClient` lookup has no such cliff. It also keeps every secret out of the process's
configuration graph, which is a smaller blast radius for something that ends up in diagnostics.

**Rejected: the community `AzureKeyVaultEmulator.Aspire.Hosting`.** A third-party container in the
development loop to emulate a service we can satisfy with user-secrets is cost without benefit —
the seam already makes the dev path honest.

**Consequence for #8:** the AppHost's `AddAzureKeyVault` resource and the host's
`AddAzureKeyVaultClient` call belong to the infrastructure change, because both need a real vault
to point at. This change ships the seam and the development implementation; #8 adds the resource,
the client registration, and the `KeyVaultSecretResolver` behind the same interface.

### D4 — Octokit, not a hand-rolled HTTP client

Octokit handles pagination, rate-limit headers, conditional requests and error mapping — all of
which we would otherwise reimplement badly. It is the official client and resolves cleanly.

**Rejected: raw `HttpClient`.** The only argument for it is dependency minimalism, and the code it
would save us writing is exactly the code most likely to be subtly wrong.

The `IBacklogConnector` seam keeps Octokit inside the GitHub implementation, so Azure DevOps
(OPN-003, later) plugs in beside it without touching callers.

### D5 — Verify the credential on save, and treat that as part of saving

UC-004 requires the credential to be verified with a live call before the Connector is stored.
This is deliberate and slightly unusual: it makes `POST` depend on an external service, and it
means saving can fail for reasons unrelated to the request body.

It is worth it. A Connector that exists but cannot read its repository is a silent failure that
surfaces much later as an empty backlog, and the operator has no way to tell "no stories" from
"broken credential". Failing at the point of configuration puts the error where the person who can
fix it is already looking. The two failure modes are reported distinctly — bad coordinates versus
bad credential — because they have different fixes.

### D6 — Polling: a hosted service, conditional requests, and a manual refresh

The poller is an `IHostedService` iterating configured Connectors on the per-project interval
(DEC-028, default 60s). Two details that matter:

- **Conditional requests: dropped during implementation, and why.** This design originally
  specified ETag conditional requests. Building it revealed that **Octokit 14's high-level API
  cannot send `If-None-Match`** — it exposes `ApiInfo.Etag` for *reading* a response's ETag and
  nothing for issuing a conditional request; there is no `NotModifiedException` either. The claim
  was wrong, and the honest options were a custom HTTP layer under Octokit or dropping the
  optimisation. Dropped: at the default interval one project costs ~60–120 of 5000 requests per
  hour, so it buys nothing today. Revisit when the project count makes the arithmetic tight. The
  spec was amended to match reality rather than the code bent to match the spec.
- **A manual refresh exists** and is what the tests drive. A background timer is the flakiest
  thing we could put in an assertion; the deterministic path is invoking the same poll directly.
  The timer's own behaviour is covered by asserting it *schedules*, not by waiting for it.

**Failure is visible, never silent.** A failed poll leaves the previous mirror intact and records
the failure against the Connector, so the UI can distinguish "nothing to show" from "we could not
look". This is the lesson from the telemetry defect, applied before rather than after.

### D7 — The mirror is a projection, and reconciles by full comparison

Each poll fetches the repository's open issues and reconciles: upsert by vendor id, and mark
absent Stories accordingly (BR-008 — the vendor is the source of truth). No attempt is made to
merge local edits, because there are none; the mirror is read-only to the application.

**Rejected: incremental sync by `updated_since`.** It is a real optimisation, and premature here —
it introduces "what did we miss" states that full reconciliation simply does not have. Revisit if a
repository is large enough to make full comparison hurt.

### D8 — The page is assembled from the existing kit, and anything missing goes to L1 first

UI work routes through the `aio-design` skill and the design system is a **CI-enforced gate**, not
a convention: raw hex, raw pixels, an unapproved font, or hardcoded copy each fail the lint lane.

The kit was checked against what this page actually needs, and it already covers all of it:

| The page needs | Existing kit |
|---|---|
| Page shell, header | `.app-shell`, `.app-header`, `.app-main` |
| Connector configuration form | `.card`, `.field`, `.label`, `.input`, `.btn`, `.btn-primary` |
| Story list with vendor ids | `.list`, `.list-row`, `.list-title`, `.mono` (tabular figures) |
| Story state and labels | `.badge-*` variants |
| Empty · loading · **failed** | `.state` and `.state-error` — which is what makes the two distinct empty states of D6 renderable without inventing anything |
| Absent values | `.empty-value` (em dash) |

**If something is genuinely missing, it is added to `docs/design-system/ui-kit/` and regenerated —
never inlined in the screen.** A component invented in a feature is how a second source of truth
starts, which is the failure the whole system exists to prevent.

Copy follows the content fundamentals: sentence case, verb-first buttons ("Configure connector",
"Refresh backlog"), the documented empty/error patterns, and the locked vocabulary — it is a
**Story**, read through a **Connector**. Relative timestamps for recency ("synced 2 minutes ago"),
absolute past a day.

### D9 — Story *fields* are normalised; Story *state values* stay the vendor's, for now

The connector seam normalises the field **set** — id, title, state, labels — so no vendor SDK type
escapes the implementation. It does **not** yet normalise the state *values*: a GitHub Story
carries GitHub's state.

This is deliberate restraint. Defining a canonical state vocabulary from a single vendor is
guessing at the mapping the second vendor will need, and Azure DevOps' work-item states are
exactly what **OPN-003** is open about. Inventing `Open`/`Closed` now would either constrain that
decision or be rewritten by it.

So: the UI renders the vendor's state string, and the cross-vendor mapping becomes part of closing
OPN-003 — where it can be decided against two real vendors instead of one imagined one. Recorded
so a reviewer sees a deferred decision rather than an oversight.

## Risks

- **A second module could reveal the boundary rules are wrong.** That is the point of D1's
  timing — better with one feature depending on it than five. If MOD002 or the ArchTests misfire,
  the fix belongs in the guardrail, not in a workaround.
- **The poller is the first background work in the app.** It must not delay startup, must not run
  in the functional test host by accident, and must handle a Connector disappearing mid-loop.
- **Rate limiting under many projects** is not a concern at one project and would be at fifty.
  ETags defer it; nothing here pretends to solve it.
