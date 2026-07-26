using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// The module-integration spec against real containers: a refresh announces exactly the facts
/// that changed, a no-op poll announces nothing, and an uncommitted transaction announces
/// nothing — the transactional-publish property that is the entire point of the outbox.
/// <para>
/// The consumer is a real handler registered through the production extension, so every test
/// exercises the full path: transactional publish → outbox → relay → handler.
/// </para>
/// </summary>
[Collection(BacklogCollection.Name)]
public class IntegrationEvents_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Configure() =>
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();

    Task Refresh() => _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

    [Fact]
    public async Task Refresh_Should_AnnounceEachStoryTheMirrorGained()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["bug"]));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Fix header", "open", []));

        await Refresh();

        var delivered = await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 2);
        delivered.Count.ShouldBe(2);
        delivered.ShouldContain(new StoryChanged(_projectId, "1", StoryChangeKind.Added));
        delivered.ShouldContain(new StoryChanged(_projectId, "2", StoryChangeKind.Added));
    }

    [Fact]
    public async Task Refresh_Should_AnnounceExactlyTheFactThatChanged()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Old title", "open", []));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Untouched", "open", []));
        await Refresh();
        await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 2);

        // One rename. The untouched Story must not be re-announced.
        fixture.Vendor.Stories[0] = new VendorStory("1", "New title", "open", []);
        await Refresh();

        var delivered = await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 3);
        delivered.Count.ShouldBe(3);
        delivered[^1].ShouldBe(new StoryChanged(_projectId, "1", StoryChangeKind.Updated));
    }

    [Fact]
    public async Task Refresh_Should_AnnounceWhatTheVendorNoLongerReturns()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Fix header", "open", []));
        await Refresh();
        await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 2);

        fixture.Vendor.Stories.RemoveAll(story => story.VendorId == "2");
        await Refresh();

        var delivered = await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 3);
        delivered[^1].ShouldBe(new StoryChanged(_projectId, "2", StoryChangeKind.Removed));
    }

    [Fact]
    public async Task ANoOpPoll_Should_AnnounceNothing()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        await Refresh();
        await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 1);

        // Nothing changed at the vendor.
        await Refresh();

        // Proving a negative needs a fence: a real change published AFTER the no-op poll. The
        // envelope topic is consumed in order (single consumer thread, one topic), so once the
        // fence arrives, anything the no-op poll had staged would already have arrived too.
        fixture.Vendor.Stories.Add(new VendorStory("fence", "Fence", "open", []));
        await Refresh();

        var delivered = await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 2);
        delivered.Count.ShouldBe(2);
        delivered[^1].VendorStoryId.ShouldBe("fence");
    }

    [Fact]
    public async Task AnUncommittedTransaction_Should_AnnounceNothing()
    {
        // The rollback case — the reason the outbox exists. Driven through the seam directly:
        // no HTTP surface fails between publish and commit on demand, and what is under test is
        // the seam's own guarantee, not an endpoint.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
            var events = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var strategy = database.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await events.BeginTransaction(
                    database.Database,
                    CancellationToken.None
                );

                await events.Publish(
                    new StoryChanged(_projectId, "never-committed", StoryChangeKind.Added)
                );

                // Disposed without Commit — the failure-after-publish path.
            });
        }

        // The artifact itself (ADR-0004): a rolled-back publish leaves no outbox row at all.
        await using (var connection = new NpgsqlConnection(fixture.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM cap.published WHERE "Content" LIKE '%never-committed%'
                """;
            (await command.ExecuteScalarAsync()).ShouldBe(0L);
        }

        // And the observable side: a committed fence arrives; the rolled-back event never does.
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("fence", "Fence", "open", []));
        await Refresh();

        var delivered = await fixture.DeliveredEvents.WaitForAtLeast(_projectId, 1);
        delivered.ShouldAllBe(@event => @event.VendorStoryId != "never-committed");
        delivered.ShouldContain(new StoryChanged(_projectId, "fence", StoryChangeKind.Added));
    }
}
