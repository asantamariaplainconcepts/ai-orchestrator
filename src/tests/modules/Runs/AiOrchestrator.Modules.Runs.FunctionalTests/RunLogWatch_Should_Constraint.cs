using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #13 — the live log window is a per-Run permission, checked by the hub itself.
/// <para>
/// It matters because the hub is the one surface the authorization decorator cannot see: it
/// dispatches no command and no query, so it declares nothing and nothing in the pipeline notices.
/// While every signed-in caller was Admin that was harmless; the slice that made authentication and
/// permission different things is what turned it into an open stream of an agent's raw output to
/// anyone who knew a Run id.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class RunLogWatch_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    Guid _projectId;
    Guid _runId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await fixture
            .CreateClient()
            .PostAsJsonAsync("/api/projects", new { name = $"p-{Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

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

    /// <summary>A host whose caller holds <paramref name="role"/> on every project, or nothing.</summary>
    WebApplicationFactory Host(ProjectRole? role) => new(fixture, role);

    [Fact]
    public async Task ACallerWithNoRoleOnTheProject_Should_NotBeAbleToWatchItsRun()
    {
        using var host = Host(role: null);
        await using var connection = host.Connect();
        await connection.StartAsync();

        // The invocation itself fails, so the caller never joins the group — a refusal after joining
        // would be a refusal that still delivers.
        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Watch", _runId)
        );

        refusal.Message.ShouldContain("do not have permission");
    }

    [Fact]
    public async Task AMemberOfTheProject_Should_BeAbleToWatchItsRun()
    {
        // Observing is what the Member bundle grants (ACT-002), so the guard must not have raised the
        // bar to Admin — a check that refuses everybody is not a check that works.
        using var host = Host(ProjectRole.Member);
        await using var connection = host.Connect();
        await connection.StartAsync();

        await Should.NotThrowAsync(() => connection.InvokeAsync("Watch", _runId));
    }

    [Fact]
    public async Task ARunThatDoesNotExist_Should_BeRefusedTheSameWay()
    {
        // Same refusal as somebody else's Run, deliberately: telling the two apart is a way to
        // enumerate Runs, which is the disclosure every other refusal in this slice avoids.
        using var host = Host(ProjectRole.Admin);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Watch", Guid.NewGuid())
        );

        refusal.Message.ShouldContain("do not have permission");
    }

    sealed record Created(Guid Id, string Name);

    /// <summary>
    /// A second host over the same containers, with the permission seam replaced. The hub connection
    /// has to reach <b>this</b> server rather than the collection fixture's, because the substitution
    /// is what the test is about.
    /// </summary>
    sealed class WebApplicationFactory : IDisposable
    {
        readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

        public WebApplicationFactory(RunsApiFixture fixture, ProjectRole? role)
        {
            _factory = fixture.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IProjectPermissions>();
                    services.AddSingleton<IProjectPermissions>(new FixedPermissions(role));
                })
            );

            // Force the server up before anything asks for its address.
            _factory.CreateClient().Dispose();
        }

        public HubConnection Connect() =>
            new HubConnectionBuilder()
                .WithUrl(
                    new Uri(_factory.Server.BaseAddress, "hubs/run-log"),
                    options =>
                    {
                        options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
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

        public void Dispose() => _factory.Dispose();
    }

    sealed class FixedPermissions(ProjectRole? role) : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(role);

        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(null);
    }
}
