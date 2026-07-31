using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #165 — the upgrade itself, exercised rather than asserted (ADR-0001).
/// <para>
/// This is the test the slice most needed. EF scaffolded the type change as <c>DropColumn</c> then
/// <c>AddColumn</c> and warned it "may result in the loss of data": applied as generated, it would
/// have discarded every hand-off configured in the deployment — every workflow edge — and left a
/// perfectly correct schema behind. No test of the new shape would have noticed, because the new
/// shape works fine empty.
/// </para>
/// <para>
/// So it runs the real migrator over a real database: up to the migration before the change, a row
/// written the way the old schema wrote it, then up to head.
/// </para>
/// </summary>
public class OutputLabelMigration_Should_Constraint : IAsyncLifetime
{
    /// <summary>The last migration that still had a single <c>OutputLabel</c> column.</summary>
    const string BeforeTheChange = "20260729150023_UniqueAutomationTrigger";

    readonly PostgreSqlContainer _database = new PostgreSqlBuilder(
        ApiServiceFixtureBase.PostgresImage
    )
        .WithDatabase("aiorchestrator")
        .Build();

    // Its own container, not the collection fixture's: this test moves the schema backwards, and a
    // shared database is the last place to do that.
    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    ProjectsDbContext Context() =>
        new(
            new DbContextOptionsBuilder<ProjectsDbContext>()
                .UseNpgsql(
                    _database.GetConnectionString(),
                    npgsql =>
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            ProjectsDbContext.Schema
                        )
                )
                .Options
        );

    [Fact]
    public async Task AConfiguredHandOff_Should_SurviveTheUpgradeToASet()
    {
        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);

            // Written the way the old schema wrote it — raw, because the model no longer has the
            // column and going through EF would be testing today's code against today's code.
            await before.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO projects.projects ("Id", "Name", "ArchivedAt")
                VALUES ({0}, 'migration-subject', NULL);

                INSERT INTO projects.automations
                  ("Id", "ProjectId", "TriggerLabel", "TriggerState", "Action", "Runtime",
                   "RequiresApproval", "Timeout", "Enabled", "RubricPath", "OutputLabel")
                VALUES ({1}, {0}, 'ai:grill', NULL, 'GrillToReady', 'ClaudeCodeHeadless',
                        false, INTERVAL '30 minutes', true, NULL, 'ai:estimate');
                """.Replace("{0}", $"'{ProjectId}'").Replace("{1}", $"'{AutomationId}'")
            );
        }

        await using (var after = Context())
        {
            await after.Database.MigrateAsync();

            var automation = await after.Automations.SingleAsync(entity =>
                entity.Id == AutomationId
            );

            // The whole point: the edge is still there, as a set of one.
            automation.OutputLabels.ShouldBe(["ai:estimate"]);
        }
    }

    [Fact]
    public async Task AnAutomationThatHandedNothingOn_Should_BecomeTheEmptySet()
    {
        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);

            await before.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO projects.projects ("Id", "Name", "ArchivedAt")
                VALUES ({0}, 'silent-subject', NULL);

                INSERT INTO projects.automations
                  ("Id", "ProjectId", "TriggerLabel", "TriggerState", "Action", "Runtime",
                   "RequiresApproval", "Timeout", "Enabled", "RubricPath", "OutputLabel")
                VALUES ({1}, {0}, 'ai:implement', NULL, 'ImplementToPullRequest', 'ClaudeCodeHeadless',
                        false, INTERVAL '30 minutes', true, NULL, NULL);
                """.Replace("{0}", $"'{SilentProjectId}'").Replace("{1}", $"'{SilentAutomationId}'")
            );
        }

        await using (var after = Context())
        {
            await after.Database.MigrateAsync();

            var automation = await after.Automations.SingleAsync(entity =>
                entity.Id == SilentAutomationId
            );

            // Not null, not a one-element set holding nothing: empty is what "ends silently" is now.
            automation.OutputLabels.ShouldBeEmpty();
        }
    }

    static readonly Guid ProjectId = new("aaaaaaaa-0000-4000-8000-000000000001");
    static readonly Guid AutomationId = new("aaaaaaaa-0000-4000-8000-000000000002");
    static readonly Guid SilentProjectId = new("bbbbbbbb-0000-4000-8000-000000000001");
    static readonly Guid SilentAutomationId = new("bbbbbbbb-0000-4000-8000-000000000002");
}
