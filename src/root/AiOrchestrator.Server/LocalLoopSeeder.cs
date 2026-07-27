using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AiOrchestrator.Server;

/// <summary>
/// Makes the local loop clickable on first boot: a demo project, its Connector and an
/// Automation on the free model (DEC-044), so a developer can label a Story and watch a Run
/// without configuring anything first.
/// <para>
/// It runs <b>only</b> when the AppHost's run composition sets <see cref="EnabledKey"/>
/// (local-agent-loop design D3). No deployed template sets it and the seeder refuses without
/// it — a property, not a promise that nobody will.
/// </para>
/// <para>
/// It writes through SQL rather than the modules' own types because those are internal to
/// their modules by design (MOD003): a dev convenience must not become the reason a boundary
/// is opened.
/// </para>
/// </summary>
sealed class LocalLoopSeeder(IConfiguration configuration, ILogger<LocalLoopSeeder> logger)
    : IHostedService
{
    public const string EnabledKey = "LocalLoop:Seed";

    /// <summary>The repository the demo Connector points at. Never invented — see D4.</summary>
    public const string RepositoryKey = "LocalLoop:Repository";

    public const string SecretNameKey = "LocalLoop:SecretName";

    public const string ProjectName = "Demo project";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue(EnabledKey, defaultValue: false))
        {
            return;
        }

        var connectionString = configuration.GetConnectionString("aiorchestratordb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var projectId = await EnsureProject(connection, cancellationToken);
        if (projectId is null)
        {
            // Already seeded — a data volume survives restarts, and duplicating on every boot
            // would be worse than not seeding at all (design D4).
            SeedLog.AlreadySeeded(logger);
            return;
        }

        await EnsureAutomation(connection, projectId.Value, cancellationToken);
        await EnsureConnector(connection, projectId.Value, cancellationToken);

        SeedLog.Seeded(logger, projectId.Value);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    async Task<Guid?> EnsureProject(NpgsqlConnection connection, CancellationToken cancellation)
    {
        await using var existing = new NpgsqlCommand(
            """SELECT "Id" FROM projects.projects WHERE "Name" = @name""",
            connection
        );
        existing.Parameters.AddWithValue("name", ProjectName);

        if (await existing.ExecuteScalarAsync(cancellation) is Guid)
        {
            return null;
        }

        var projectId = Guid.CreateVersion7();
        await using var insert = new NpgsqlCommand(
            """INSERT INTO projects.projects ("Id", "Name") VALUES (@id, @name)""",
            connection
        );
        insert.Parameters.AddWithValue("id", projectId);
        insert.Parameters.AddWithValue("name", ProjectName);
        await insert.ExecuteNonQueryAsync(cancellation);

        return projectId;
    }

    async Task EnsureAutomation(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellation
    )
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO projects.automations
                ("Id", "ProjectId", "TriggerLabel", "TriggerState", "Action", "Runtime",
                 "RequiresApproval", "Timeout", "Enabled")
            VALUES (@id, @projectId, 'ai:implement', NULL, 'ImplementToPullRequest', 'OpenCode',
                    false, @timeout, true)
            """,
            connection
        );
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("timeout", TimeSpan.FromMinutes(30));
        await command.ExecuteNonQueryAsync(cancellation);
    }

    async Task EnsureConnector(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellation
    )
    {
        var repository = configuration[RepositoryKey];

        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
        {
            // A Connector for a repository the developer does not control would fail on the
            // first poll and look like a bug in the product (design D4).
            SeedLog.NoRepositoryConfigured(logger, RepositoryKey);
            return;
        }

        var parts = repository.Split('/', 2);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO backlog.connectors
                ("Id", "ProjectId", "Vendor", "Owner", "Repository", "SecretName")
            VALUES (@id, @projectId, 1, @owner, @name, @secretName)
            """,
            connection
        );
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("owner", parts[0]);
        command.Parameters.AddWithValue("name", parts[1]);
        command.Parameters.AddWithValue(
            "secretName",
            configuration[SecretNameKey] ?? "local-github-pat"
        );
        await command.ExecuteNonQueryAsync(cancellation);
    }
}

static partial class SeedLog
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Seeded the local demo project {ProjectId} — label a Story with ai:implement to close the loop"
    )]
    public static partial void Seeded(ILogger logger, Guid projectId);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Debug,
        Message = "The local demo project already exists; nothing seeded"
    )]
    public static partial void AlreadySeeded(ILogger logger);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Warning,
        Message = "No repository configured at {Key} — the demo project has no Connector. Set it (owner/name) and add its PAT to user secrets to close the loop"
    )]
    public static partial void NoRepositoryConfigured(ILogger logger, string key);
}
