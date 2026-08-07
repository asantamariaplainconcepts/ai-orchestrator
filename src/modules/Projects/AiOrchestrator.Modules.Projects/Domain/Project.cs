using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// A unit of configuration: one Connector, its Automations, its caps (BC-001).
/// Only the name exists at this stage — Connector and Automation arrive as product changes.
/// </summary>
sealed class Project : Aggregate
{
    Project() { }

    Project(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// When this Project was retired, or null while it is live (#121, design D3). A timestamp
    /// rather than a flag because the list wants to say <i>when</i>, and a boolean would need a
    /// second column the moment anybody asked.
    /// <para>
    /// Archiving stops new work — no polling, no matching, no manual Run — and stops nothing
    /// else: what its agents already did stays readable, because BR-014 makes that record the
    /// audit trail rather than clutter.
    /// </para>
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>
    /// The runtime an Automation with no explicit one resolves to at execution time
    /// (project-runtimes). Null means the deployment default — absence is an answer here, the
    /// same way an unset Automation runtime is.
    /// </summary>
    public string? DefaultRuntime { get; private set; }

    /// <summary>
    /// Credential secret <b>names</b> per runtime (BR-010: names stored, values never). The
    /// project's billing identity where one exists; the deployment's config supplies the
    /// fallback.
    /// </summary>
    public List<ProjectRuntimeCredential> RuntimeCredentials { get; private set; } = [];

    /// <summary>
    /// Full replace, like the Automation update: the form always shows every field, so a field
    /// it omitted would silently reset — the same reasoning #151 recorded.
    /// </summary>
    public void ConfigureRuntimes(
        string? defaultRuntime,
        IReadOnlyDictionary<string, string> credentialNames
    )
    {
        DefaultRuntime = string.IsNullOrWhiteSpace(defaultRuntime) ? null : defaultRuntime.Trim();
        RuntimeCredentials.Clear();
        foreach (var (runtime, secretName) in credentialNames)
        {
            if (!string.IsNullOrWhiteSpace(secretName))
            {
                RuntimeCredentials.Add(new ProjectRuntimeCredential(runtime, secretName.Trim()));
            }
        }
    }

    public static Project Create(string name) => new(name);

    /// <summary>Idempotent: archiving an archived Project keeps the original moment.</summary>
    public void Archive(DateTimeOffset at) => ArchivedAt ??= at;

    public void Restore() => ArchivedAt = null;
}

/// <summary>One runtime's credential name on a Project — a name, never a value (BR-010).</summary>
sealed class ProjectRuntimeCredential
{
    ProjectRuntimeCredential() { }

    public ProjectRuntimeCredential(string runtime, string secretName)
    {
        Runtime = runtime;
        SecretName = secretName;
    }

    public Guid Id { get; private set; }

    public string Runtime { get; private set; } = string.Empty;

    public string SecretName { get; private set; } = string.Empty;
}
