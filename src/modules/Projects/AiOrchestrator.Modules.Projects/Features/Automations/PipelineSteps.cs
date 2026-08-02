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
    /// The steps a button may install a starter for. Only tiers that require nothing: a tier
    /// declaring <c>requires</c> is opt-in by construction (#190, design D2), and installing one
    /// unprompted would push a methodology into a repository whose team never chose it. Adoption
    /// still recognises those steps — the difference is between reading a file and writing one.
    /// </summary>
    public static IReadOnlyList<PipelineStep> Installable =>
        [.. All.Where(step => step.Tier.Requires is null)];

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
