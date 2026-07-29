namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// Turns an Automation's prompt <b>name</b> into a repository path, using the project's prompts
/// directory (#150, design D6).
/// <para>
/// One resolution site, and that is the point rather than tidiness: the composed path is what a
/// refusal reports, so a misconfigured directory gives itself away instead of looking like a missing
/// file. Two sites composing it would eventually disagree about what to report.
/// </para>
/// </summary>
static class PromptPath
{
    /// <summary>
    /// Where prompts live when a project has said nothing — the Platform's own <c>ai/</c> home, so a
    /// project that configures nothing still resolves names.
    /// </summary>
    internal const string DefaultDirectory = "ai/prompts";

    /// <summary>
    /// The directory as stored: no leading or trailing slash, so composition is one rule and never
    /// produces <c>ai/prompts//x.md</c>. Blank means the default.
    /// </summary>
    internal static string NormalizeDirectory(string? directory) =>
        string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory.Trim().Trim('/');

    /// <summary>
    /// <c>Failure</c> is null on success. Refuses rather than normalizes, because the directory is
    /// meant to bound where prompts come from and a boundary that can be stepped over is decoration
    /// — the issue put repository-absolute paths out of scope, and one resolution rule only holds
    /// while the other route is closed.
    /// </summary>
    internal static (string? Path, string? Failure) Resolve(string? directory, string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, "this automation names no prompt file");
        }

        if (trimmed.StartsWith('/') || trimmed.StartsWith('\\'))
        {
            return (
                null,
                $"prompt name '{trimmed}' is an absolute path; names resolve inside the project's "
                    + "prompts directory"
            );
        }

        // Both separators, because a name is typed by a person and '\' would otherwise smuggle a
        // '..' segment past a '/'-only check.
        var segments = trimmed.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return (
                null,
                $"prompt name '{trimmed}' leaves the prompts directory; names resolve inside it"
            );
        }

        return ($"{NormalizeDirectory(directory)}/{string.Join('/', segments)}", null);
    }
}
