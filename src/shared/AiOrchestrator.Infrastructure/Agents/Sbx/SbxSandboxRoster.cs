using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// What this host owns on the machine, and how it finds out — one definition, used by everything that
/// enumerates or deletes a sandbox (#311).
/// <para>
/// <b>`aio-*` is shorthand, not the boundary.</b> The claimed prefixes are exactly the two
/// <see cref="SbxSandboxLifecycle"/> reaps. Other <c>aio-</c> names on the machine — <c>aio-carry-*</c>
/// staging directories, <c>aio-workspace-*</c> archives — are host paths and not sandboxes at all, so a
/// wildcard would claim things that are not ours to claim.
/// </para>
/// <para>
/// The predicate lives here rather than beside the reaper because two callers now depend on it: the
/// startup sweep, which <b>deletes</b> what matches, and the sandboxes surface, which <b>enters</b> it.
/// Those two answers must never drift — a surface that could enter what the sweep does not manage would
/// be a way into machines this product does not own, and a sweep that deleted what the surface cannot
/// see would be a leak nobody could diagnose.
/// </para>
/// </summary>
static class SbxSandboxRoster
{
    /// <summary>
    /// The names this host claims. <c>aio-probe-*</c> is a readiness probe's sandbox, created every
    /// thirty seconds; <c>aio-run-*</c> is a Run's.
    /// </summary>
    static readonly string[] ClaimedPrefixes = ["aio-probe-", "aio-run-"];

    /// <summary>Whether this host owns the sandbox by that name, and may therefore enter or reap it.</summary>
    public static bool Claims(string? name) =>
        name is not null
        && ClaimedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// The sandboxes on this machine that this host claims, with the status sbx reports for each.
    /// Returns empty when the CLI cannot answer — the callers treat "cannot tell" as "nothing to act
    /// on", which is what keeps a broken daemon from reaping or listing anything.
    /// </summary>
    public static async Task<IReadOnlyList<SbxSandboxEntry>> Claimed(
        SbxCli cli,
        CancellationToken cancellationToken
    )
    {
        // `--json` rather than the human table. The table's columns shift — PORTS is empty for a
        // sandbox that publishes none — so position-based parsing reads the workspace as the port for
        // exactly the sandboxes a Run creates without a preview. Verified against the real CLI.
        var listed = await cli.Run(["ls", "--json"], SbxCli.Brief, cancellationToken);
        if (listed.ExitCode != 0)
        {
            return [];
        }

        Listing? listing;
        try
        {
            listing = JsonSerializer.Deserialize<Listing>(listed.Stdout, Format);
        }
        catch (JsonException)
        {
            // A CLI whose output shape moved. Same answer as a failed exit: nothing to act on, rather
            // than an exception out of a startup sweep or a read.
            return [];
        }

        return
        [
            .. (listing?.Sandboxes ?? [])
                .Where(sandbox => Claims(sandbox.Name))
                .Select(sandbox => new SbxSandboxEntry(
                    sandbox.Name!,
                    sandbox.Status ?? "unknown",
                    sandbox.Workspaces?.FirstOrDefault()
                )),
        ];
    }

    static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    /// <summary>`sbx ls --json`'s shape, verified against the real CLI on 2026-08-11.</summary>
    sealed record Listing([property: JsonPropertyName("sandboxes")] Sandbox[]? Sandboxes);

    sealed record Sandbox(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("workspaces")] string[]? Workspaces
    );
}

/// <summary>
/// One sandbox on this machine, as the host sees it before any Run is attributed to it.
/// <paramref name="Status"/> is sbx's own word (observed: <c>running</c>, <c>stopped</c>) and is carried
/// rather than interpreted, because entering a stopped sandbox starts it and the surface must say so.
/// </summary>
sealed record SbxSandboxEntry(string Name, string Status, string? Workspace);
