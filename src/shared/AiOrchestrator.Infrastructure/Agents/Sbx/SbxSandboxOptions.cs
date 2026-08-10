namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Everything the sbx host knows, decided in composition. Deliberately NOT IConfiguration
/// (design D7): the driver cannot read a connection string it has no access to.
/// </summary>
sealed class SbxSandboxOptions
{
    public const string DefaultCommand = "sbx";

    /// <summary>
    /// Explicit rather than sbx's 50%-of-host-RAM default (spike H5), which two concurrent
    /// sandboxes would use to exhaust the machine.
    /// </summary>
    public const string DefaultMemory = "4g";

    /// <summary>
    /// GitHub by default: every Run's agent does git work, and this is the credential whose
    /// injection the spike proved end to end. The AI provider is habitat-dependent — a free
    /// model (DEC-044) needs none — so it is named by configuration or not at all.
    /// </summary>
    public static readonly string[] DefaultInjectedSecrets = ["github"];

    public required string CommandPath { get; init; }
    public required string Memory { get; init; }
    public required IReadOnlyList<string> InjectedSecrets { get; init; }

    /// <summary>
    /// The commands sbx provides an agent template for — the images that actually contain the
    /// CLI. Configured rather than hardcoded so a new runtime does not need a code change, and
    /// defaulted to this product's two runtimes (DEC-012, DEC-044), whose names sbx happens to
    /// share because both are naming the same CLIs.
    /// </summary>
    public IReadOnlyList<string> AgentTemplates { get; init; } = DefaultAgentTemplates;

    /// <summary>
    /// Credential files, relative to the machine owner's home, to copy into each sandbox — the
    /// dev loop's opt-in (#288). Empty means carry nothing, which is every habitat but the dev
    /// loop, because a carried session is readable by whatever runs in the sandbox.
    /// <para>
    /// Files only, and only credential ones. Observed 2026-08-08: opencode's whole session is
    /// <c>.local/share/opencode/auth.json</c> (950 bytes); its <c>.config/opencode</c> tree is
    /// caches. Claude Code on macOS has no file at all — its credential is in the system
    /// keychain — so nothing here can carry it, and the readiness panel says so instead.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SessionFiles { get; init; } = [];

    /// <summary>
    /// Runtimes whose session a copy cannot reach because the machine keeps it in a keychain
    /// rather than a file. Observed on macOS 2026-08-08: Claude Code has no
    /// <c>~/.claude/.credentials.json</c>, and copying its directory in produced "Not logged in".
    /// <para>
    /// Configuration rather than a constant because it is platform-dependent — the same CLI on
    /// Linux writes a credentials file, which this list would then wrongly exclude. Defaulted for
    /// the platform this was measured on, and overridable by whoever measures another.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> KeychainRuntimes { get; init; } =
        OperatingSystem.IsMacOS() ? ["claude"] : [];

    /// <summary>
    /// What the dev loop carries when it opts in: opencode's credential file, and GitHub
    /// Copilot's, which arrives with its runtime (#243). Both are files; Claude Code's macOS
    /// session is not, deliberately absent rather than silently ineffective.
    /// </summary>
    public static readonly string[] DefaultSessionFiles =
    [
        ".local/share/opencode/auth.json",
        ".config/github-copilot/apps.json",
        ".config/github-copilot/hosts.json",
    ];

    public static readonly string[] DefaultAgentTemplates = ["claude", "opencode"];

    /// <summary>Warm creation was ~4.5s in the spike; a cold image pull is minutes.</summary>
    public TimeSpan CreateTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long a CLI-in-the-template verdict stands before it is asked again. Far longer than
    /// the panel's 30s cadence on purpose: this answer belongs to the image, and re-creating a
    /// sandbox every cycle to re-learn it would spend seconds of every thirty on a question
    /// whose answer cannot have changed.
    /// </summary>
    public TimeSpan CliProbeInterval { get; init; } = TimeSpan.FromMinutes(15);
}
