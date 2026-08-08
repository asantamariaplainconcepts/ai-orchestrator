namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Turns a model-listing CLI's stdout into names (#291). One place, because both hosts ask the
/// same question of the same CLIs and two parsers would eventually disagree about the same output.
/// <para>
/// Deliberately forgiving about shape and strict about emptiness: `opencode models` prints one
/// name per line (observed 2026-08-08 — 41 on the host, 495 inside a sandbox), and anything a
/// terminal adds around that — blank lines, stray whitespace — is noise rather than a model.
/// </para>
/// </summary>
static class AgentModelListing
{
    public static IReadOnlyList<string> Parse(string stdout) =>
        [
            .. stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                // A model name has no spaces; a line that does is the CLI talking, not listing.
                .Where(line => line.Length > 0 && !line.Contains(' '))
                .Distinct(StringComparer.Ordinal),
        ];
}
