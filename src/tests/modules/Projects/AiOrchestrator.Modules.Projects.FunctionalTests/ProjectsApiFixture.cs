using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.SharedFunctionalTests;
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

    /// <summary>
    /// Mutable stub in a shared fixture, so it is restored here — the leak this hook exists for
    /// (#13). A document one class seeded and never cleared would make another class's "you do not
    /// have this starter yet" quietly wrong.
    /// </summary>
    public override async Task ResetDatabase()
    {
        Documents.Reset();
        await base.ResetDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentReader>();
            services.AddSingleton<IDocumentReader>(Documents);
        });
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
