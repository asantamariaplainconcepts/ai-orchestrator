namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// The pipeline steps a repository's own files can be recognised as (#229, design D3).
/// <para>
/// A step is a catalogue starter that carries wiring — the product invents no methodology here
/// either, so a fork that edits the manifest changes what adoption recognises without touching
/// this file. The name is the whole mapping: <c>implement.md</c> is the implement step whether it
/// sits in <c>ai/prompts</c> or in <c>.claude/commands/ds</c>, and <c>sprint-notes.md</c> is not a
/// step at all and gets no Automation. Inventing a trigger from an unrecognised filename would
/// create a label nobody applies and an Automation that never fires.
/// </para>
/// </summary>
static class PipelineSteps
{
    /// <summary>
    /// Every recognisable step, catalogue order. Includes steps from tiers that declare a
    /// <c>requires</c>: a repository that already holds <c>grill.md</c> has adopted that
    /// methodology, and recognising the file is reading what is there, not imposing it.
    /// </summary>
    public static IReadOnlyList<PipelineStep> All =>
        [
            .. StarterCatalogue.Tiers.SelectMany(tier =>
                tier.Prompts.Where(prompt => prompt.Automation is not null)
                    .Select(prompt => new PipelineStep(prompt, tier))
            ),
        ];

    /// <summary>
    /// The steps a button may install a starter for: tiers that require nothing, plus the tiers this
    /// caller has <paramref name="consented"/> to by name (#269).
    /// <para>
    /// A tier declaring <c>requires</c> is opt-in by construction (#190, design D2), and installing
    /// one <i>unprompted</i> would push a methodology into a repository whose team never chose it. A
    /// consent that is off by default, names the tier and states the paths it writes is not
    /// unprompted — it is the prompt. What the rule still forbids is the silent case, which is why an
    /// empty consent leaves a gated tier uninstallable rather than merely unselected.
    /// </para>
    /// <para>
    /// Adoption is unaffected either way: <see cref="All"/> recognises every tier's files, because
    /// reading a file a team wrote was never the act in question.
    /// </para>
    /// </summary>
    /// <param name="consented">
    /// Tier ids, compared <b>exactly</b>. These are catalogue identifiers a caller echoes back from
    /// discovery, not labels a human types — so this is deliberately not the case-insensitive BR-003
    /// comparison triggers use, and should not be "fixed" into one.
    /// </param>
    public static IReadOnlyList<PipelineStep> Installable(IReadOnlyCollection<string>? consented) =>
        [
            .. All.Where(step =>
                step.Tier.Requires is null
                || (
                    consented is not null
                    && consented.Contains(step.Tier.Id, StringComparer.Ordinal)
                )
            ),
        ];

    /// <summary>
    /// The step a discovered file is, or null. Matched on the file's stem against both the
    /// catalogue's source name and its saved name, because a team that took a starter has
    /// <c>aio-grill.md</c> while a team that wrote its own has <c>grill.md</c> — the same step.
    /// </summary>
    public static PipelineStep? Match(string fileName)
    {
        var stem = Stem(fileName);

        return string.IsNullOrEmpty(stem)
            ? null
            : All.FirstOrDefault(step =>
                string.Equals(Stem(step.Prompt.File), stem, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Stem(step.Prompt.SaveAs), stem, StringComparison.OrdinalIgnoreCase)
            );
    }

    static string Stem(string fileName)
    {
        var name = fileName.Trim();
        var slash = name.LastIndexOfAny(['/', '\\']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
    }
}

/// <summary>
/// One recognisable step: the catalogue prompt that defines it and the tier it came from. The
/// tier travels because installability is a tier property, not a prompt one.
/// </summary>
sealed record PipelineStep(StarterPrompt Prompt, StarterTier Tier)
{
    public StarterAutomation Wiring => Prompt.Automation!;

    public string Trigger => Wiring.Trigger;
}
