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
    /// Tiers in manifest order, prompts in manifest order. Ordering is content, not presentation:
    /// the portable tier comes first because it is the one that answers "this project has no
    /// prompts", and a surface sorting them alphabetically would put the methodology first.
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
                                wiring.OutputLabels
                            )
                            : null
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
        IReadOnlyList<ManifestPrompt> Prompts
    );

    sealed record ManifestPrompt(
        string File,
        string SaveAs,
        string Purpose,
        string Assumes,
        ManifestAutomation? Automation = null
    );

    sealed record ManifestAutomation(
        string Trigger,
        bool RequiresApproval,
        IReadOnlyList<string> OutputLabels
    );
}

/// <summary>
/// <paramref name="Requires"/> is null for a tier that assumes only the repository. Non-null is the
/// labelling design D2 exists for: the reference set this change started from was five-sixths
/// unportable, and presenting it as though it were portable would move the failure from a sentence
/// on the screen to an agent that cannot find a file.
/// </summary>
sealed record StarterTier(
    string Id,
    string Title,
    string Summary,
    string? Requires,
    IReadOnlyList<StarterPrompt> Prompts
);

/// <summary>
/// <paramref name="Assumes"/> is the same discipline one level down: a prompt that quietly needs
/// push access is a prompt whose first failure is confusing.
/// <para>
/// <paramref name="SaveAs"/> is the name the file takes in a project, and it is not always
/// <paramref name="File"/>: the portable and workflow tiers both ship an <c>implement.md</c>, and
/// without distinct saved names they would land on the same path and only one could ever be taken.
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

/// <summary>One wired starter: what the created Automation triggers on, gates, and hands on.</summary>
sealed record StarterAutomation(
    string Trigger,
    bool RequiresApproval,
    IReadOnlyList<string> OutputLabels
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
