using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
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
    /// Saving the confirmed prompts directory (#229), faked at the same Contracts seam — the real
    /// write is a Connector column this module does not own.
    /// </summary>
    internal StubPromptDirectoryWriter Directories { get; }

    /// <summary>
    /// Mutable stub in a shared fixture, so it is restored here — the leak this hook exists for
    /// (#13). A document one class seeded and never cleared would make another class's "you do not
    /// have this starter yet" quietly wrong.
    /// </summary>
    public ProjectsApiFixture() =>
        Directories = new StubPromptDirectoryWriter(Documents, Connector);

    public override async Task ResetDatabase()
    {
        Documents.Reset();
        Connector.Reset();
        Workspace.Reset();
        Directories.Reset();
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
            services.RemoveAll<IPromptDirectoryWriter>();
            services.AddSingleton<IPromptDirectoryWriter>(Directories);
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

/// <summary>
/// Records the confirmed directory and makes the rest of the fixture agree with it, because that
/// is what the real save does: everything read afterwards resolves under the directory just saved,
/// and a stub that stored the value without moving the reads would prove nothing about adoption.
/// </summary>
sealed class StubPromptDirectoryWriter(StubDocumentReader documents, StubConnectorReader connector)
    : IPromptDirectoryWriter
{
    /// <summary>Every directory confirmed, in order — "nothing was saved" is asserted on this.</summary>
    public List<string> Saved { get; } = [];

    public void Reset() => Saved.Clear();

    public Task<bool> UseDirectory(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        if (connector.Snapshot is null)
        {
            return Task.FromResult(false);
        }

        var normalized = directory.Trim().Trim('/');
        Saved.Add(normalized);
        documents.Directory = normalized;
        connector.Snapshot = connector.Snapshot with { PromptDirectory = normalized };
        return Task.FromResult(true);
    }
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
            CredentialReference.Named("acme-pat"),
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

    /// <summary>
    /// Paths the repository already holds, created in the prepared workspace before anything is
    /// written (#269). This is how the existing-file rule is exercised for a prerequisite: the
    /// installer decides against the clone, so the only honest way to test it is to put a file in the
    /// clone.
    /// </summary>
    public List<string> ExistingFiles { get; } = [];

    /// <summary>
    /// What each published path contained. Needed because a file the repository already had is
    /// *present* at publish time whether or not this action touched it — so "left alone" can only be
    /// asserted on content, never on absence.
    /// </summary>
    public Dictionary<string, string> PublishedContents { get; } = new(StringComparer.Ordinal);

    /// <summary>The content a seeded existing file holds, so a test can assert it survived.</summary>
    public const string TheProjectsOwnContent = "the project's own content";

    public void Reset()
    {
        PrepareError = null;
        PublishError = null;
        PreparedBranch = null;
        PublishedAsDraft = null;
        PublishedFiles.Clear();
        PublishedContents.Clear();
        ExistingFiles.Clear();
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

        var root = Directory.CreateTempSubdirectory("install-ws-").FullName;

        // Seed what the repository already has, so a caller that only writes where nothing exists is
        // tested against a clone that really holds the file.
        foreach (var existing in ExistingFiles)
        {
            var path = Path.Combine(root, existing.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, TheProjectsOwnContent);
        }

        return Task.FromResult<ErrorOr<PreparedWorkspace>>(
            new PreparedWorkspace(coordinates, root, branch)
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
        PublishedContents.Clear();
        if (Directory.Exists(workspace.Path))
        {
            foreach (
                var file in Directory.EnumerateFiles(
                    workspace.Path,
                    "*",
                    SearchOption.AllDirectories
                )
            )
            {
                var relative = Path.GetRelativePath(workspace.Path, file).Replace('\\', '/');
                PublishedFiles.Add(relative);
                PublishedContents[relative] = File.ReadAllText(file);
            }
        }

        return Task.FromResult<ErrorOr<PublishedChange>>(
            PublishError is { } error ? error : new PublishedChange(PullRequestUrl)
        );
    }
}

/// <summary>One candidate directory's contents, as a test arranges them (#229).</summary>
sealed record StubDirectory(List<string> Files, List<string> Subdirectories)
{
    public static StubDirectory Of(params string[] files) => new([.. files], []);

    public static StubDirectory Holding(params string[] subdirectories) =>
        new([], [.. subdirectories]);
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

    /// <summary>
    /// What each candidate directory holds (#229). Cleared below in the same edit that added it —
    /// the lesson from the stub whose two new properties leaked between tests two changes ago.
    /// </summary>
    public Dictionary<string, StubDirectory> Directories { get; } = new(StringComparer.Ordinal);

    /// <summary>Every directory a discovery probed, in order.</summary>
    public List<string> Listed { get; } = [];

    public void Reset()
    {
        Connected = true;
        Directory = "ai/prompts";
        Documents.Clear();
        Reads.Clear();
        Directories.Clear();
        Listed.Clear();
    }

    public Task<DirectoryListing> ListPromptFiles(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        Listed.Add(directory);

        if (!Connected)
        {
            return Task.FromResult(
                new DirectoryListing(
                    directory,
                    [],
                    [],
                    Absent: false,
                    "this project has no connector"
                )
            );
        }

        return Task.FromResult(
            Directories.TryGetValue(directory, out var entries)
                ? new DirectoryListing(
                    directory,
                    entries.Files,
                    entries.Subdirectories,
                    Absent: false,
                    Failure: null
                )
                : new DirectoryListing(directory, [], [], Absent: true, Failure: null)
        );
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
