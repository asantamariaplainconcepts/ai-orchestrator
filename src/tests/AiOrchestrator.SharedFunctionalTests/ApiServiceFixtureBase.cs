using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace AiOrchestrator.SharedFunctionalTests;

/// <summary>
/// The shared functional-test host: the real Server driven through <see cref="WebApplicationFactory{T}"/>
/// against real PostgreSQL and Azurite containers — no in-memory substitutes, because a substitute
/// proves nothing about the database the product actually runs on.
/// <para>
/// One container stack per module via <c>ICollectionFixture</c>: per-class fixtures overwhelm the
/// runner. Between tests <see cref="ResetDatabase"/> (Respawn) restores a clean slate.
/// </para>
/// </summary>
public abstract class ApiServiceFixtureBase : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Canonical registry names. Behind a mirror (or on an air-gapped machine), point Testcontainers
    // at it with TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX instead of editing these — CI pulls them as-is.
    public const string PostgresImage = "postgres:18-alpine";
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";

    readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("aiorchestrator")
        .Build();

    readonly AzuriteContainer _storage = new AzuriteBuilder(AzuriteImage).Build();

    NpgsqlConnection? _respawnConnection;
    Respawner? _respawner;

    public string DatabaseConnectionString => _database.GetConnectionString();

    public string StorageConnectionString => _storage.GetConnectionString();

    /// <summary>Schemas this module owns, reset between tests.</summary>
    protected abstract string[] SchemasToReset { get; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _storage.StartAsync());

        // Boot the host, then migrate explicitly. The Server deliberately never migrates itself —
        // in the composed application that step is the AppHost's `migrations` resource — so this
        // fixture, which owns its own database lifecycle, runs the same MigrateModules call that
        // resource runs. It must happen before Respawner is created: Respawner captures the
        // schema graph up front, and against an empty database it would capture nothing and
        // reset nothing.
        CreateClient().Dispose();
        await Services.MigrateModules(ModuleRegistration.Discover());

        _respawnConnection = new NpgsqlConnection(DatabaseConnectionString);
        await _respawnConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            _respawnConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = SchemasToReset,
            }
        );
    }

    /// <summary>
    /// Virtual so a fixture holding mutable stubs restores them here too (#13). A stub a test class
    /// changed and never put back is a shared fixture leaking into the next class — and once
    /// authorization gated every operation rather than one endpoint, a leaked Member role turned
    /// eight unrelated tests red in a module that had not been touched.
    /// </summary>
    public virtual async Task ResetDatabase()
    {
        if (_respawner is not null && _respawnConnection is not null)
        {
            await _respawner.ResetAsync(_respawnConnection);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");
        builder.UseSetting("ConnectionStrings:aiorchestratordb", DatabaseConnectionString);
        builder.UseSetting("ConnectionStrings:queues", StorageConnectionString);
    }

    // WebApplicationFactory already owns a ValueTask DisposeAsync; xUnit's IAsyncLifetime wants a
    // Task-returning one. The explicit implementation delegates so there is a single teardown path.
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public override async ValueTask DisposeAsync()
    {
        if (_respawnConnection is not null)
        {
            await _respawnConnection.DisposeAsync();
        }

        await base.DisposeAsync();
        await Task.WhenAll(_database.DisposeAsync().AsTask(), _storage.DisposeAsync().AsTask());
        GC.SuppressFinalize(this);
    }

    /// <summary>Resolves a scoped service from the running host — used by fixtures to run migrations.</summary>
    protected T GetRequiredService<T>()
        where T : notnull
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
