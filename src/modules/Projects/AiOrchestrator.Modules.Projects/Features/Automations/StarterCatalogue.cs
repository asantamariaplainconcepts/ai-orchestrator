using System.Reflection;
using System.Text.Json;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// The starter prompts this product offers a project that has none (#190).
/// <para>
/// Read from embedded resources rather than held as strings, for the reason the criterion demands
/// (design D3): a starter has to carry real frontmatter, because the promise is that a file taken
/// from here behaves identically whether this product runs it or a local agent runner does — and
/// that is only checkable when the artifact <i>is</i> the file. The bytes a test loads are the bytes
/// the endpoint serves.
/// </para>
/// <para>
/// <b>The product never writes any of this anywhere</b> (design D1). It is content to copy. That is
/// the whole decision #190 turned on, and the absence of a write path is how it is kept.
/// </para>
/// </summary>
static class StarterCatalogue
{
    const string Prefix = "AiOrchestrator.Modules.Projects.Starter.";

    static readonly Assembly Assembly = typeof(StarterCatalogue).Assembly;

    static readonly Lazy<IReadOnlyList<StarterTier>> Loaded = new(Load);

    /// <summary>
    /// Tiers in manifest order, prompts in manifest order. Ordering is content, not presentation: a
    /// surface that sorted them alphabetically would decide for the catalogue which methodology a
    /// reader meets first, which is the manifest's decision to make.
    /// </summary>
    public static IReadOnlyList<StarterTier> Tiers => Loaded.Value;

    static IReadOnlyList<StarterTier> Load()
    {
        var manifest =
            JsonSerializer.Deserialize<Manifest>(
                Text($"{Prefix}manifest.json"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            ) ?? throw new InvalidOperationException("The starter manifest did not deserialize.");

        return
        [
            .. manifest.Tiers.Select(tier => new StarterTier(
                tier.Id,
                tier.Title,
                tier.Summary,
                tier.Requires,
                [
                    .. tier.Prompts.Select(prompt => new StarterPrompt(
                        prompt.File,
                        prompt.SaveAs,
                        prompt.Purpose,
                        prompt.Assumes,
                        Text($"{Prefix}{tier.Id}.{prompt.File}"),
                        prompt.Automation is { } wiring
                            ? new StarterAutomation(
                                wiring.Trigger,
                                wiring.RequiresApproval,
                                wiring.ToStage,
                                wiring.Marks
                            )
                            : null
                    )),
                ],
                [
                    .. tier.Prerequisites.Select(prerequisite => new StarterPrerequisite(
                        prerequisite.Path,
                        Text($"{Prefix}{tier.Id}.prerequisites.{prerequisite.File}")
                    )),
                ]
            )),
        ];
    }

    /// <summary>
    /// A manifest entry naming a file that is not embedded throws here, at first read, rather than
    /// serving a tier with a hole in it. The test that enumerates the manifest is what turns that
    /// into a red build instead of a runtime surprise.
    /// </summary>
    static string Text(string resource)
    {
        using var stream =
            Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"The starter catalogue names '{resource}', which is not embedded. "
                    + "Every manifest entry must have a file beside it."
            );

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    sealed record Manifest(IReadOnlyList<ManifestTier> Tiers);

    sealed record ManifestTier(
        string Id,
        string Title,
        string Summary,
        string? Requires,
        IReadOnlyList<ManifestPrompt> Prompts,
        IReadOnlyList<ManifestPrerequisite>? Prerequisites = null
    )
    {
        /// <summary>Absent and empty are the same answer here: a tier that needs nothing extra.</summary>
        public IReadOnlyList<ManifestPrerequisite> Prerequisites { get; } = Prerequisites ?? [];
    }

    sealed record ManifestPrerequisite(string File, string Path);

    sealed record ManifestPrompt(
        string File,
        string SaveAs,
        string Purpose,
        string Assumes,
        ManifestAutomation? Automation = null
    );

    /// <summary>
    /// A wired starter, as the manifest spells it (#310). <paramref name="ToStage"/> is the transition
    /// the step claims — absent for one that hands on to nobody, which is how the tier's last step and
    /// its standalone steps stay expressible. <paramref name="Marks"/> is absent for the ordinary case:
    /// a mark names no stage and moves nothing, and no catalogue step needs one today.
    /// </summary>
    sealed record ManifestAutomation(
        string Trigger,
        bool RequiresApproval,
        string? ToStage = null,
        IReadOnlyList<string>? Marks = null
    )
    {
        public IReadOnlyList<string> Marks { get; } = Marks ?? [];
    }
}

/// <summary>
/// <paramref name="Requires"/> is null for a tier that assumes only the repository. Non-null is the
/// labelling #190's design D2 exists for: presenting a tier as though it assumed only the repository
/// would move the failure from a sentence on the screen to an agent that cannot find a file.
/// <para>
/// Since #269 the sentence has a second job. It is no longer only a warning about what a reader must
/// already have — it is the text of a consent, because consenting to the tier installs
/// <paramref name="Prerequisites"/> too. A tier that declares what it assumes is therefore both
/// opt-in and self-supplying.
/// </para>
/// </summary>
sealed record StarterTier(
    string Id,
    string Title,
    string Summary,
    string? Requires,
    IReadOnlyList<StarterPrompt> Prompts,
    /// <summary>
    /// The files this tier's prompts read that a fresh repository has not got (#269). Empty for a
    /// tier that needs nothing beyond its prompts.
    /// </summary>
    IReadOnlyList<StarterPrerequisite> Prerequisites
);

/// <summary>
/// One file a tier needs in place before its prompts can run. <paramref name="Path"/> is
/// repository-relative and fixed — unlike a prompt's <c>SaveAs</c>, it is never resolved against the
/// Connector's prompt directory, because a process document does not live in a prompt folder.
/// <para>
/// The source name and the target path are decoupled on purpose: one placeholder file fills two empty
/// directories, and a source called <c>gitkeep.txt</c> lands as <c>.gitkeep</c>.
/// </para>
/// </summary>
sealed record StarterPrerequisite(string Path, string Content);

/// <summary>
/// <paramref name="Assumes"/> is the same discipline one level down: a prompt that quietly needs
/// push access is a prompt whose first failure is confusing.
/// <para>
/// <paramref name="SaveAs"/> is the name the file takes in a project, and it is not always
/// <paramref name="File"/>: a tier's <c>implement.md</c> lands as <c>aio-implement.md</c>, so a
/// repository can hold a starter beside a file of its own with the obvious name. Two tiers shipping
/// the same source name was the original reason (#190) and remains the one that would bite again.
/// </para>
/// </summary>
sealed record StarterPrompt(
    string File,
    string SaveAs,
    string Purpose,
    string Assumes,
    string Content,
    /// <summary>
    /// The default wiring set-up-defaults creates (#212), or null for a prompt that is content
    /// only. Catalogue data beside the prompt it belongs to — the product hardcodes no
    /// methodology, and a fork that wants different defaults edits the manifest.
    /// </summary>
    StarterAutomation? Automation = null
);

/// <summary>
/// One wired starter: what the created Automation triggers on, gates, and hands on.
/// <para>
/// Since #310 handing on is a <b>claimed transition</b> rather than an output label (design D10).
/// <paramref name="ToStage"/> null means the step claims none — it acts, it may mark the Story, and the
/// flow ends there. Installing a tier therefore creates the project's lifecycle stages as a consequence
/// of claiming, which is how a new project gets a lifecycle without "seed a default lifecycle" coming
/// into scope.
/// </para>
/// </summary>
sealed record StarterAutomation(
    string Trigger,
    bool RequiresApproval,
    string? ToStage,
    IReadOnlyList<string> Marks
);

/// <summary>
/// Serialization-time shape. <paramref name="TargetPath"/> and <paramref name="AlreadyPresent"/> are
/// both null when the project has no Connector — <b>unknown</b>, not absent (design D6). The same
/// distinction BR-011 makes about an unmeasured cost, for the same reason: a null rendered as
/// "you do not have this" is a claim nobody checked.
/// </summary>
sealed record StarterPromptResponse(
    string File,
    string SaveAs,
    string Purpose,
    string Assumes,
    string Content,
    string? TargetPath,
    bool? AlreadyPresent
);

sealed record StarterTierResponse(
    string Id,
    string Title,
    string Summary,
    string? Requires,
    IReadOnlyList<StarterPromptResponse> Prompts
);
