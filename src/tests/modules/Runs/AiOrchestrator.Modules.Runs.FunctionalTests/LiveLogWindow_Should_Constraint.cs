using System.Diagnostics;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Execution;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit.Abstractions;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #106 — the live window, measured rather than asserted in principle. The whole path is real:
/// the writer commits, Postgres announces, the portal's listener reads and pushes, and a client
/// on the other end of the hub receives.
/// </summary>
[Collection(RunsCollection.Name)]
public class LiveLogWindow_Should_Constraint(RunsApiFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _runId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        // A Run to watch. Its Automation never executes here — this exercises the log path, not
        // the execution path.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = Run.Create(
            _projectId,
            "1",
            Guid.NewGuid(),
            RunLocus.Sandbox,
            DateTimeOffset.UtcNow
        );
        database.Runs.Add(run);
        await database.SaveChangesAsync();
        _runId = run.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A hub connection over the test server, which has no real socket to dial.</summary>
    async Task<HubConnection> Watch(Guid runId, TaskCompletionSource<string[]> received)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(fixture.Server.BaseAddress, "hubs/run-log"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
                    options.WebSocketFactory = null;
                    options.Transports = Microsoft
                        .AspNetCore
                        .Http
                        .Connections
                        .HttpTransportType
                        .LongPolling;
                }
            )
            .Build();

        // The frame carries where it starts (#144, design D5) so a client that subscribed before
        // its first read can drop the overlap. Binding to the shape asserts the contract, and the
        // previous `string[]` binding silently received nothing when the shape changed — which is
        // how these two tests caught it.
        connection.On<LogFrame>("lines", frame => received.TrySetResult(frame.Lines));

        await connection.StartAsync();
        await connection.InvokeAsync("Watch", runId);
        return connection;
    }

    void WriteLine(string line)
    {
        var writer = new RunLogWriter(
            _runId,
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance
        );
        writer.Write(line);
        // Disposing drains and commits, which is what fires the notification.
        writer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ALine_Should_ReachAWatcherInUnderASecond()
    {
        var received = new TaskCompletionSource<string[]>();
        await using var connection = await Watch(_runId, received);

        var clock = Stopwatch.StartNew();
        WriteLine("the runtime said something");

        var arrived = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        clock.Stop();

        arrived.ShouldBe(received.Task, "no line reached the watcher within ten seconds");
        (await received.Task).ShouldContain("the runtime said something");
        // Printed, not only asserted: acceptance criterion 1 asks for the figure, and a bound
        // that passes tells you nothing about how much room is left under it.
        output.WriteLine($"line reached the watcher in {clock.ElapsedMilliseconds}ms");
        clock.ElapsedMilliseconds.ShouldBeLessThan(
            1000,
            $"the line took {clock.ElapsedMilliseconds}ms to reach a watcher"
        );
    }

    [Fact]
    public async Task TwoWatchers_Should_BothReceiveTheLine()
    {
        var first = new TaskCompletionSource<string[]>();
        var second = new TaskCompletionSource<string[]>();
        await using var one = await Watch(_runId, first);
        await using var two = await Watch(_runId, second);

        WriteLine("said once, heard twice");

        var both = Task.WhenAll(first.Task, second.Task);
        (await Task.WhenAny(both, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBe(both);
        (await first.Task).ShouldContain("said once, heard twice");
        (await second.Task).ShouldContain("said once, heard twice");
    }

    [Fact]
    public async Task TheRecord_Should_BeCompleteWithoutAnyWatcher()
    {
        // Criterion 4: nothing about a Run depends on delivery. Nobody is watching here.
        WriteLine("nobody is listening");

        var log = await _client.GetFromJsonAsync<RunLogResponse>(
            $"/api/projects/{_projectId}/runs/{_runId}/log"
        );

        log!.Content.ShouldContain("nobody is listening");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record RunLogResponse(string Content, bool Complete);
}

/// <summary>The pushed frame's shape, as the hub sends it.</summary>
sealed record LogFrame(int From, string[] Lines);
