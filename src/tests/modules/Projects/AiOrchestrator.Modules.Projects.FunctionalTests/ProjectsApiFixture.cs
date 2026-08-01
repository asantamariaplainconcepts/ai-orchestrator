using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using ErrorOr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// One container stack for the whole Projects module — shared through
/// <see cref="ProjectsCollection"/>, because a stack per test class overwhelms the runner.
/// </summary>
public sealed class ProjectsApiFixture : ApiServiceFixtureBase
{
    protected override string[] SchemasToReset => [ProjectsDbContext.Schema];

    /// <summary>
    /// The repository's documents, as this module may read them (#190). Stubbed at the Contracts
    /// seam rather than at the vendor, because that is the surface this module actually holds — the
    /// real reader lives in Backlog, and reaching past it would be testing somebody else's code.
    /// </summary>
    internal StubDocumentReader Documents { get; } = new();

    /// <summary>The Connector as installs read it (#214) — stubbed at the same Contracts seam.</summary>
    internal StubConnectorReader Connector { get; } = new();

    /// <summary>The publish ceremony faked at its seam, exactly as the Runs tests fake it.</summary>
    internal StubInstallWorkspace Workspace { get; } = new();

    /// <summary>
    /// Mutable stub in a shared fixture, so it is restored here — the leak this hook exists for
    /// (#13). A document one class seeded and never cleared would make another class's "you do not
    /// have this starter yet" quietly wrong.
    /// </summary>
    public override async Task ResetDatabase()
    {
        Documents.Reset();
        Connector.Reset();
        Workspace.Reset();
        await base.ResetDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentReader>();
            services.AddSingleton<IDocumentReader>(Documents);
            services.RemoveAll<IConnectorReader>();
            services.AddSingleton<IConnectorReader>(Connector);
            services.RemoveAll<ICodeWorkspace>();
            services.AddSingleton<ICodeWorkspace>(Workspace);
            services.RemoveAll<BuildingBlocks.Secrets.ISecretResolver>();
            services.AddSingleton<BuildingBlocks.Secrets.ISecretResolver>(new StubSecretResolver());
        });
    }
}

/// <summary>Resolves every name to the same in-memory value — BR-010's seam, faked at it.</summary>
sealed class StubSecretResolver : BuildingBlocks.Secrets.ISecretResolver
{
    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default) =>
        Task.FromResult("stub-token");
}

/// <summary>A Connector snapshot the test decides; null is the no-Connector project.</summary>
sealed class StubConnectorReader : IConnectorReader
{
    public ConnectorSnapshot? Snapshot { get; set; }

    public void Reset() =>
        Snapshot = new ConnectorSnapshot(
            "GitHub",
            "acme",
            "portal",
            "acme-pat",
            "Repository",
            null
        );

    public Task<ConnectorSnapshot?> Find(
        Guid projectId,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Snapshot);
}

/// <summary>
/// The install's workspace, scripted: no git, no vendor — what the tests are about is the use
/// case's refusals and its one write, not how a branch is pushed.
/// </summary>
sealed class StubInstallWorkspace : ICodeWorkspace
{
    public Error? PrepareError { get; set; }

    public Error? PublishError { get; set; }

    public string PullRequestUrl { get; set; } = "https://github.com/acme/portal/pull/7";

    /// <summary>The branch the install asked for — determinism is asserted on this.</summary>
    public string? PreparedBranch { get; private set; }

    /// <summary>Whether the PR was opened as a draft — the spec's whole point.</summary>
    public bool? PublishedAsDraft { get; private set; }

    /// <summary>Files present in the workspace at publish time, relative to its root.</summary>
    public List<string> PublishedFiles { get; } = [];

    public void Reset()
    {
        PrepareError = null;
        PublishError = null;
        PreparedBranch = null;
        PublishedAsDraft = null;
        PublishedFiles.Clear();
        PullRequestUrl = "https://github.com/acme/portal/pull/7";
    }

    public Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        Guid runId,
        string token,
        CancellationToken cancellationToken
    ) => Prepare(coordinates, $"run/{runId}", token, cancellationToken);

    public Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        string branch,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (PrepareError is { } error)
        {
            return Task.FromResult<ErrorOr<PreparedWorkspace>>(error);
        }

        PreparedBranch = branch;
        return Task.FromResult<ErrorOr<PreparedWorkspace>>(
            new PreparedWorkspace(
                coordinates,
                Directory.CreateTempSubdirectory("install-ws-").FullName,
                branch
            )
        );
    }

    public Task<ErrorOr<PublishedChange>> Publish(
        PreparedWorkspace workspace,
        string title,
        string body,
        string token,
        CancellationToken cancellationToken,
        bool draft = false
    )
    {
        PublishedAsDraft = draft;
        PublishedFiles.Clear();
        if (Directory.Exists(workspace.Path))
        {
            PublishedFiles.AddRange(
                Directory
                    .EnumerateFiles(workspace.Path, "*", SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(workspace.Path, file).Replace('\\', '/'))
            );
        }

        return Task.FromResult<ErrorOr<PublishedChange>>(
            PublishError is { } error ? error : new PublishedChange(PullRequestUrl)
        );
    }
}

/// <summary>
/// A reader whose answers a test decides. <see cref="Connected"/> false is the no-Connector case:
/// the real reader returns a failure with <b>no resolved path</b>, because resolving one needs the
/// project's prompts directory and there is no Connector holding it — and that absence is what makes
/// presence <i>unknown</i> rather than absent (design D6).
/// </summary>
sealed class StubDocumentReader : IDocumentReader
{
    public bool Connected { get; set; } = true;

    public string Directory { get; set; } = "ai/prompts";

    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    /// <summary>Every prompt name asked for, in order — what bounds the reads is asserted on this.</summary>
    public List<string> Reads { get; } = [];

    public void Reset()
    {
        Connected = true;
        Directory = "ai/prompts";
        Documents.Clear();
        Reads.Clear();
    }

    public Task<DocumentResult> Read(
        Guid projectId,
        string path,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Result(path, resolved: null));

    public Task<DocumentResult> ReadPrompt(
        Guid projectId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        Reads.Add(name);

        if (!Connected)
        {
            return Task.FromResult(
                new DocumentResult(null, "this project has no connector configured")
            );
        }

        var path = $"{Directory}/{name}";
        return Task.FromResult(Result(path, resolved: path));
    }

    DocumentResult Result(string path, string? resolved) =>
        Documents.TryGetValue(path, out var content)
            ? new DocumentResult(content, null, resolved)
            : new DocumentResult(null, $"'{path}' was not found", resolved);
}

[CollectionDefinition(Name)]
public sealed class ProjectsCollection : ICollectionFixture<ProjectsApiFixture>
{
    public const string Name = "Projects";
}
