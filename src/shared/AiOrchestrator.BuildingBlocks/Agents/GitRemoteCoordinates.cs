using System.Diagnostics.CodeAnalysis;

namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What a folder's `origin` says about where its Project lives (#347, UC-003): the vendor and the
/// coordinates, derived so an operator does not retype what the repository already knows.
/// <para>
/// The vendor travels as a <b>string</b> matching the backlog vendor's own name, because this lives
/// below the module that owns that enum and the Contracts boundary already speaks vendors this way
/// (<c>ConnectorSnapshot.Vendor</c>). Parsing here rather than in either module keeps one answer for
/// two callers — the create-time derivation and its tests.
/// </para>
/// </summary>
public static class GitRemoteCoordinates
{
    public const string GitHubVendor = "GitHub";
    public const string AzureDevOpsVendor = "AzureDevOps";

    /// <summary>
    /// Derives coordinates from a remote URL, in either the SSH or the HTTPS form. Returns false
    /// for a remote matching neither vendor — which is a fact to report, never a reason to refuse
    /// the Project: the operator types the coordinates instead and the flow proceeds.
    /// </summary>
    public static bool TryParse(
        string? remoteUrl,
        [NotNullWhen(true)] out RemoteCoordinates? parsed
    )
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var (host, path) = Split(remoteUrl.Trim());

        if (host is null || path is null)
        {
            return false;
        }

        // `.git` is a suffix of the transport, not part of any coordinate; trailing slashes come
        // from hand-typed remotes.
        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            // owner/repo — GitHub's issues and code share one repository, so there is no third
            // coordinate to fill.
            return segments.Length == 2
                && Yield(
                    new RemoteCoordinates(GitHubVendor, segments[0], segments[1], null),
                    out parsed
                );
        }

        // dev.azure.com/{org}/{project}/_git/{repo}
        if (host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return segments is [var org, var project, "_git", var repo]
                && Yield(new RemoteCoordinates(AzureDevOpsVendor, org, project, repo), out parsed);
        }

        // {org}.visualstudio.com/{project}/_git/{repo} — the legacy host, where the organisation is
        // in the hostname rather than the path.
        if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var org = host[..host.IndexOf('.', StringComparison.Ordinal)];

            return segments is [var project, "_git", var repo]
                && Yield(new RemoteCoordinates(AzureDevOpsVendor, org, project, repo), out parsed);
        }

        return false;
    }

    static bool Yield(RemoteCoordinates value, out RemoteCoordinates? parsed)
    {
        parsed = value;
        return true;
    }

    /// <summary>
    /// Host and path, from either form. SSH remotes are `git@host:path` — not a URI, which is why
    /// this is hand-split rather than handed to <see cref="Uri"/>; `ssh://` remotes are, and go
    /// through the same path as HTTPS.
    /// </summary>
    static (string? Host, string? Path) Split(string remote)
    {
        if (
            remote.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || remote.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || remote.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
        )
        {
            return Uri.TryCreate(remote, UriKind.Absolute, out var uri)
                ? (uri.Host, uri.AbsolutePath)
                : (null, null);
        }

        var at = remote.IndexOf('@', StringComparison.Ordinal);
        var colon = remote.IndexOf(':', StringComparison.Ordinal);

        // `git@github.com:owner/repo.git`. The colon must follow the `@`, or this is not the scp-like
        // form and guessing at it would invent coordinates from an unrelated string.
        return at >= 0 && colon > at
            ? (remote[(at + 1)..colon], remote[(colon + 1)..])
            : (null, null);
    }
}

/// <summary>
/// The coordinates a folder yields. <paramref name="CodeRepository"/> is null for GitHub and the
/// repository inside the project for Azure DevOps — the three fields
/// <c>AzureDevOpsBacklogConnector</c> actually reads, in the shape it reads them.
/// </summary>
public sealed record RemoteCoordinates(
    string Vendor,
    string Owner,
    string Repository,
    string? CodeRepository
);
