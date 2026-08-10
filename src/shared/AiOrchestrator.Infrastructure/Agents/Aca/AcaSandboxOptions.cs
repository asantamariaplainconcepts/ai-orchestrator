namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>
/// What a habitat declares to execute Runs in Azure sandboxes (#296). Two of these are corrections
/// to platform defaults rather than preferences — see <see cref="AcaAgentProcessHost"/> — and the
/// composition refuses a habitat that leaves them unsaid, because a deployment that forgets is a
/// deployment whose agent runs unrestricted.
/// </summary>
sealed class AcaSandboxOptions
{
    public const string DefaultCommand = "aca";

    /// <summary>
    /// The public prebuilt disk carrying Claude Code — measured 2026-08-08: Ubuntu 26.04 with
    /// `claude` 2.1.198 as a native binary and git 2.55.0, so a deployment needs no image of ours.
    /// </summary>
    public const string DefaultDisk = "claude";

    /// <summary>
    /// GitHub, because every Run's agent does git work. Everything else a habitat needs is its own
    /// to declare: a deny-default policy is only as good as the list it is paired with.
    /// </summary>
    public static readonly string[] DefaultEgressAllow = ["github.com", "api.github.com"];

    public required string CommandPath { get; init; }

    /// <summary>The Project's own group (design D4), so a Run bills as its own Project (#244).</summary>
    public required string SandboxGroup { get; init; }

    /// <summary>
    /// A **public** prebuilt disk, by name. Mutually exclusive with <see cref="DiskId"/>.
    /// </summary>
    public required string Disk { get; init; }

    /// <summary>
    /// A disk this deployment built itself, by id — `aca sandboxgroup disk create --image …`
    /// turns any container image into one.
    /// <para>
    /// **Why this exists (measured 2026-08-09).** The public disks carry `claude` and `copilot`
    /// and nothing else, so this product's other runtime — opencode, and the free model that
    /// makes the local loop need no AI credential at all — could not run on this substrate. A
    /// disk built from `node:22-bookworm` takes `opencode-ai@1.18.6` and runs it, so the gap was
    /// never the platform: `sandbox create` takes `--disk` for a public name and `--disk-id` for
    /// a private one, and this host only ever passed the first.
    /// </para>
    /// </summary>
    public string? DiskId { get; init; }

    public required IReadOnlyList<string> EgressAllow { get; init; }

    /// <summary>
    /// The group's typed credential ids this launcher attaches to every sandbox it creates —
    /// `github-copilot`, `anthropic-claude` — obtained from
    /// <c>aca sandboxgroup credential create</c>.
    /// <para>
    /// **Ids, never values (BR-010).** The platform holds the token and injects it at its own
    /// egress boundary; nothing enters the sandbox, which is the property that makes this
    /// substrate worth adopting over passing environment values.
    /// </para>
    /// <para>
    /// Empty is legitimate and means an agent that authenticates some other way — but a sandbox
    /// created without one has no credential at all, and until 2026-08-09 that was every sandbox
    /// this host made: `create` never passed `--credential`. Design D4 promised per-Project
    /// credentials and the code did not ask for them, which no fixture could notice because the
    /// stand-in was never going to authenticate anything.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Credentials { get; init; } = [];

    /// <summary>
    /// How often the poll loop asks a sandbox what its agent has written. Frequent enough that a
    /// Member watching sees it work (UC-027), sparse enough not to spend an exec per second across
    /// the thirty minutes BR-005 allows.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// One CLI call, not one Run. Comfortably above the ~1 s an exec round-trips and comfortably
    /// below the ceiling measured at 50–60 s, which this host never approaches because it polls.
    /// </summary>
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// How many times a sandbox creation refused for authorization is tried again before the Run
    /// fails. Six at ten seconds covers the ~1 minute of propagation the spike measured, with
    /// room either side; a deployment whose grant is genuinely missing still fails, one minute
    /// later, naming the role.
    /// </summary>
    public int AuthorizationAttempts { get; init; } = 6;

    public TimeSpan AuthorizationRetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A property of the disk image rather than of the moment, so it is asked rarely — creating a
    /// microVM to re-answer it would spend seconds on a fact that has not moved.
    /// </summary>
    public TimeSpan CliProbeInterval { get; init; } = TimeSpan.FromMinutes(15);
}
