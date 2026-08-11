using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #310 AC 10 — the upgrade itself, exercised rather than asserted (ADR-0001).
/// <para>
/// This is the test the slice most needed, for the reason #165's own migration test records: what EF
/// scaffolds for this change is two <c>AddColumn</c>s, which leave every <c>ToStage</c> null and every
/// stage list empty. No test of the new shape would notice, because the new shape works fine empty —
/// the deployment would simply come up with every workflow edge gone and every board a single column.
/// </para>
/// <para>
/// So it runs the real migrator over a real database: up to the migration before the change, rows
/// written the way the old schema wrote them, then up to head. The shapes seeded are the ones AC 10
/// names — a chain of three, a standalone Automation, an output label matching nothing, an edge that
/// differs only in case, and an Automation handing to several — because a migration verified on one
/// tidy chain is verified on the deployment nobody has.
/// </para>
/// </summary>
public class ClaimedTransitionMigration_Should_Constraint : IAsyncLifetime
{
    /// <summary>The last migration before an Automation claimed a transition.</summary>
    const string BeforeTheChange = "20260808132549_AutomationModel";

    // Its own container, not the collection fixture's: this test moves the schema backwards, and a
    // shared database is the last place to do that (the same reason #165's migration test gives).
    readonly PostgreSqlContainer _database = new PostgreSqlBuilder(
        ApiServiceFixtureBase.PostgresImage
    )
        .WithDatabase("aiorchestrator")
        .Build();

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
    public async Task TheChainTheBoardDrew_Should_BecomeClaimsAndAnOrderedLifecycle()
    {
        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);
            await before.Database.ExecuteSqlRawAsync(TheChain);
        }

        await using (var after = Context())
        {
            await after.Database.MigrateAsync();

            var automations = await after
                .Automations.Where(entity => entity.ProjectId == ChainProject)
                .ToListAsync();
            var grill = automations.Single(entity => entity.Id == Grill);
            var propose = automations.Single(entity => entity.Id == Propose);
            var implement = automations.Single(entity => entity.Id == Implement);
            var estimate = automations.Single(entity => entity.Id == Estimate);
            var review = automations.Single(entity => entity.Id == Review);
            var sync = automations.Single(entity => entity.Id == Sync);

            // The edge became the claim, and nothing was left behind in the marks.
            grill.ToStage.ShouldBe("ai:propose");
            grill.OutputLabels.ShouldBeEmpty();

            // The case-differing edge: 'AI:IMPLEMENT' matched 'ai:implement' because the comparison
            // folds case (DEC-056). Carried case-sensitively, this edge would have been dropped —
            // which is exactly what AC 10's second bullet warns about. The stored spelling is the
            // stage's own, not the one the output label happened to use.
            propose.ToStage.ShouldBe("ai:implement");
            propose.OutputLabels.ShouldBe(["needs-review"]);

            // An output label matching no sibling trigger is a mark, and claims nothing.
            implement.ToStage.ShouldBeNull();
            implement.OutputLabels.ShouldBe(["done-marker"]);

            // DEC-053's standalone Automation: it acts on its own, hands on to nobody, and stays
            // expressible because ToStage is nullable (design D3).
            estimate.ToStage.ShouldBeNull();
            estimate.OutputLabels.ShouldBeEmpty();

            // 'ai:sync' belongs to a *disabled* Automation, and AC 10 matches enabled siblings only,
            // so this is a mark rather than a claim.
            review.ToStage.ShouldBeNull();
            review.OutputLabels.ShouldBe(["ai:sync"]);

            // A disabled Automation's hand-off is configuration too, so it is preserved as a claim.
            // Its from-stage is deliberately *not* a stage — the stage list holds what the board
            // drew, and the board draws enabled Automations only (see the migration's own note).
            sync.ToStage.ShouldBe("ai:grill");

            var project = await after.Projects.SingleAsync(entity => entity.Id == ChainProject);

            // The order the board drew, not alphabetical and not creation order: the flow first
            // (roots, then whatever each hands to), then the Automations outside it
            // (KanbanBoard.tsx:110-137). 'ai:sync' is absent because the board never drew it.
            project.LifecycleStages.ShouldBe([
                "ai:grill",
                "ai:propose",
                "ai:implement",
                "ai:estimate",
                "ai:review",
            ]);
        }
    }

    [Fact]
    public async Task AnAutomationHandingToSeveral_Should_ClaimTheFirstAndKeepTheRestAsMarks()
    {
        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);
            await before.Database.ExecuteSqlRawAsync(TheBranch);
        }

        await using (var after = Context())
        {
            await after.Database.MigrateAsync();

            var triage = await after.Automations.SingleAsync(entity => entity.Id == Triage);

            // Branching is unrepresentable now (AC 13), so the second edge cannot survive as an
            // edge. It survives as a *mark*, in its original position among the remaining labels —
            // the label is not lost, only its meaning changes, which is the honest reading of a
            // model with nowhere to put a second transition.
            triage.ToStage.ShouldBe("ai:build");
            triage.OutputLabels.ShouldBe(["ai:audit", "needs-docs"]);

            var project = await after.Projects.SingleAsync(entity => entity.Id == BranchProject);
            project.LifecycleStages.ShouldBe(["ai:triage", "ai:build", "ai:audit"]);
        }
    }

    /// <summary>
    /// The reverse, exercised rather than described. A <c>Down</c> nobody ran is a rollback plan
    /// nobody has: this one rewrites an array column, so a wrong cast would only be discovered by the
    /// person rolling a deployment back.
    /// <para>
    /// It is lossy on purpose, and the test says which loss: the claim comes back as the first output
    /// label — where the old walk reads the edge from, so the canvas derives the flow that was
    /// configured — while the stage <i>order</i> is gone, because the old shape has nowhere to put it.
    /// That is ADR-0022's whole subject, asserted rather than promised.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheReverse_Should_PutTheClaimBackAsTheFirstLabelAndLoseTheOrder()
    {
        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);
            await before.Database.ExecuteSqlRawAsync(TheChain);
        }

        await using (var forwards = Context())
        {
            await forwards.Database.MigrateAsync();
        }

        await using (var backwards = Context())
        {
            await backwards.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);

            var labels = await backwards
                .Database.SqlQueryRaw<string>(
                    """
                    SELECT array_to_string("OutputLabels", ',') AS "Value"
                    FROM projects.automations
                    WHERE "Id" = '{propose}'
                    """.Replace("{propose}", Propose.ToString())
                )
                .SingleAsync();

            // The edge is at the front again, ahead of the mark that travelled with it. Note the
            // spelling: the round trip returns the *stage's*, not the 'AI:IMPLEMENT' somebody typed —
            // the same label to the vendor (DEC-056), and the only spelling the new shape held.
            labels.ShouldBe("ai:implement,needs-review");

            // And the loss, named: there is no column left in which an order could have survived.
            var stages = await backwards
                .Database.SqlQueryRaw<long>(
                    """
                    SELECT count(*) AS "Value"
                    FROM information_schema.columns
                    WHERE table_schema = 'projects'
                      AND table_name = 'projects'
                      AND column_name = 'LifecycleStages'
                    """
                )
                .SingleAsync();

            stages.ShouldBe(0);
        }
    }

    /// <summary>
    /// AC 10's last clause, as a measurement rather than a comment: <b>the count of configured
    /// hand-offs before equals the count after</b>. The before-count is read out of the old schema by
    /// the same question the board asks — how many Automations hand work to an enabled sibling — so
    /// the test is not comparing the migration against itself.
    /// </summary>
    [Fact]
    public async Task EveryConfiguredHandOff_Should_SurviveAsExactlyOneClaim()
    {
        long handOffsBefore;
        long labelsBefore;

        await using (var before = Context())
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeTheChange);
            await before.Database.ExecuteSqlRawAsync(TheChain);
            await before.Database.ExecuteSqlRawAsync(TheBranch);

            handOffsBefore = await before
                .Database.SqlQueryRaw<long>(
                    """
                    SELECT count(*) AS "Value"
                    FROM projects.automations a
                    WHERE EXISTS (
                        SELECT 1
                        FROM unnest(a."OutputLabels") AS label(value)
                        WHERE EXISTS (
                            SELECT 1
                            FROM projects.automations sibling
                            WHERE sibling."ProjectId" = a."ProjectId"
                              AND sibling."Id" <> a."Id"
                              AND sibling."Enabled"
                              AND lower(sibling."TriggerLabel") = lower(label.value)
                        )
                    )
                    """
                )
                .SingleAsync();

            labelsBefore = await before
                .Database.SqlQueryRaw<long>(
                    """
                    SELECT COALESCE(sum(cardinality("OutputLabels")), 0) AS "Value"
                    FROM projects.automations
                    """
                )
                .SingleAsync();
        }

        // The seeded shapes hold four hand-offs: grill → propose, propose → implement (the
        // case-differing one), triage → build, and the disabled sync → grill. Stated here so a future
        // edit to the seed cannot quietly make the assertion below vacuous.
        handOffsBefore.ShouldBe(4);

        await using (var after = Context())
        {
            await after.Database.MigrateAsync();

            var claims = await after
                .Database.SqlQueryRaw<long>(
                    """
                    SELECT count(*) AS "Value"
                    FROM projects.automations
                    WHERE "ToStage" IS NOT NULL
                    """
                )
                .SingleAsync();

            // AC 10: the count of configured hand-offs before equals the count after.
            claims.ShouldBe(handOffsBefore);

            var labelsAfter = await after
                .Database.SqlQueryRaw<long>(
                    """
                    SELECT COALESCE(sum(cardinality("OutputLabels")), 0)
                         + count(*) FILTER (WHERE "ToStage" IS NOT NULL) AS "Value"
                    FROM projects.automations
                    """
                )
                .SingleAsync();

            // And the stronger statement, which is what "loses no configured hand-off" has to mean
            // once a second edge can only become a mark: every label that was there is still there,
            // as either the one claim or a mark. A migration that dropped a label while keeping the
            // claim count would pass the clause above and still have lost configuration.
            labelsAfter.ShouldBe(labelsBefore);
        }
    }

    /// <summary>
    /// A chain of three, a standalone Automation, a label matching nothing, an edge differing only in
    /// case, an enabled Automation pointing at a disabled sibling's trigger, and a disabled
    /// Automation with a hand-off of its own. Written raw, because the model no longer has the old
    /// shape and going through EF would be testing today's code against today's code.
    /// <para>
    /// The empty label set is <c>ARRAY[]::character varying(200)[]</c> rather than Postgres' own
    /// <c>'{}'</c>: <c>ExecuteSqlRawAsync</c> puts the string through <c>string.Format</c>, so a brace
    /// is a format placeholder and the literal throws before the database ever sees it.
    /// </para>
    /// </summary>
    static string TheChain =>
        """
            INSERT INTO projects.projects ("Id", "Name", "ArchivedAt")
            VALUES ('{chain}', 'chain-subject', NULL);

            INSERT INTO projects.automations
              ("Id", "ProjectId", "TriggerLabel", "TriggerState", "Action", "Runtime",
               "RequiresApproval", "Timeout", "Enabled", "PromptPath", "OutputLabels")
            VALUES
              ('{grill}', '{chain}', 'ai:grill', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL,
               ARRAY['ai:propose']::character varying(200)[]),
              ('{propose}', '{chain}', 'ai:propose', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL,
               ARRAY['AI:IMPLEMENT', 'needs-review']::character varying(200)[]),
              ('{implement}', '{chain}', 'ai:implement', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL,
               ARRAY['done-marker']::character varying(200)[]),
              ('{estimate}', '{chain}', 'ai:estimate', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL, ARRAY[]::character varying(200)[]),
              ('{review}', '{chain}', 'ai:review', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL,
               ARRAY['ai:sync']::character varying(200)[]),
              ('{sync}', '{chain}', 'ai:sync', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', false, NULL,
               ARRAY['ai:grill']::character varying(200)[]);
            """.Replace("{chain}", ChainProject.ToString()).Replace(
            "{grill}",
            Grill.ToString()
        ).Replace("{propose}", Propose.ToString()).Replace(
            "{implement}",
            Implement.ToString()
        ).Replace("{estimate}", Estimate.ToString()).Replace("{review}", Review.ToString()).Replace(
            "{sync}",
            Sync.ToString()
        );

    /// <summary>One Automation handing to two enabled siblings, plus a mark that matches nothing.</summary>
    static string TheBranch =>
        """
            INSERT INTO projects.projects ("Id", "Name", "ArchivedAt")
            VALUES ('{branch}', 'branch-subject', NULL);

            INSERT INTO projects.automations
              ("Id", "ProjectId", "TriggerLabel", "TriggerState", "Action", "Runtime",
               "RequiresApproval", "Timeout", "Enabled", "PromptPath", "OutputLabels")
            VALUES
              ('{triage}', '{branch}', 'ai:triage', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL,
               ARRAY['ai:build', 'ai:audit', 'needs-docs']::character varying(200)[]),
              ('{build}', '{branch}', 'ai:build', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL, ARRAY[]::character varying(200)[]),
              ('{audit}', '{branch}', 'ai:audit', NULL, 'RepositoryPrompt', 'ClaudeCodeHeadless',
               false, INTERVAL '30 minutes', true, NULL, ARRAY[]::character varying(200)[]);
            """.Replace("{branch}", BranchProject.ToString()).Replace(
            "{triage}",
            Triage.ToString()
        ).Replace("{build}", Build.ToString()).Replace("{audit}", Audit.ToString());

    static readonly Guid ChainProject = new("cccccccc-0000-4000-8000-000000000001");
    static readonly Guid Grill = new("cccccccc-0000-4000-8000-000000000002");
    static readonly Guid Propose = new("cccccccc-0000-4000-8000-000000000003");
    static readonly Guid Implement = new("cccccccc-0000-4000-8000-000000000004");
    static readonly Guid Estimate = new("cccccccc-0000-4000-8000-000000000005");
    static readonly Guid Review = new("cccccccc-0000-4000-8000-000000000006");
    static readonly Guid Sync = new("cccccccc-0000-4000-8000-000000000007");

    static readonly Guid BranchProject = new("dddddddd-0000-4000-8000-000000000001");
    static readonly Guid Triage = new("dddddddd-0000-4000-8000-000000000002");
    static readonly Guid Build = new("dddddddd-0000-4000-8000-000000000003");
    static readonly Guid Audit = new("dddddddd-0000-4000-8000-000000000004");
}
