namespace AiOrchestrator.Modules.Projects.Contracts;

/// <summary>
/// The Project's runtime resolution inputs, for the Runs module (project-runtimes, #244): the
/// default a runtime-less Automation resolves to, and the credential secret names per runtime
/// (BR-010 — names, never values). Asked per execution and never cached, the same freshness rule
/// <see cref="IProjectCatalog"/> states: settings changed while the application runs apply to the
/// next Run, not to a stale snapshot.
/// </summary>
public interface IProjectRuntimeSettings
{
    Task<ProjectRuntimeResolution> Resolve(
        Guid projectId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// <paramref name="DefaultRuntime"/> null means the deployment default — absence is an answer.
/// <paramref name="CredentialNames"/> is keyed by runtime name; a runtime absent from it falls
/// back to the deployment's configured name.
/// </summary>
public sealed record ProjectRuntimeResolution(
    string? DefaultRuntime,
    IReadOnlyDictionary<string, string> CredentialNames
)
{
    public static ProjectRuntimeResolution None { get; } =
        new(null, new Dictionary<string, string>());
}
