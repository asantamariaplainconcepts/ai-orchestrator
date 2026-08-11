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
/// #304 — a terminal refuses in three distinguishable ways, and this is what makes that a property
/// rather than a claim.
/// <para>
/// Each refusal has a different remedy, which is the whole reason they must not collapse into one:
/// a Member told "you may not" about a habitat that hosts no terminal would go and ask for access
/// that cannot help them, and a finished Run offered a disabled control would promise something no
/// Run can keep.
/// </para>
/// <para>
/// Checked in the hub for the reason <see cref="RunLogWatch_Should_Constraint"/> records: a hub
/// dispatches nothing through the pipeline, so the decorator guarding every other read never sees
/// it. That mattered for a read; this surface executes commands inside a sandbox carrying the
/// machine owner's own session, so the same omission would be worse by a wide margin.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class RunTerminalRefusal_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

    WebApplicationFactory Host(ProjectRole? role) => new(fixture, role);

    [Fact]
    public async Task ACallerWithNoRoleOnTheProject_Should_BeRefusedOnPermission()
    {
        using var host = Host(role: null);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Open", _runId, 80, 24)
        );

        refusal.Message.ShouldContain("do not have permission");
    }

    [Fact]
    public async Task ARunThatDoesNotExist_Should_BeRefusedTheSameWayAsSomebodyElsesRun()
    {
        // The disclosure every refusal in this slice avoids: telling "no such Run" apart from "not
        // your project" is a way to enumerate Runs.
        using var host = Host(ProjectRole.Admin);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Open", Guid.NewGuid(), 80, 24)
        );

        refusal.Message.ShouldContain("do not have permission");
    }

    [Fact]
    public async Task AHabitatThatHostsNoTerminal_Should_SaySo_RatherThanRefuseOnPermission()
    {
        // The test tier hosts no sandboxes, which is exactly the deployed habitat's answer
        // (ADR-0021). A Member who may attach still gets nothing here — and must be told why in a
        // sentence that does not send them asking for access.
        using var host = Host(ProjectRole.Member);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Open", _runId, 80, 24)
        );

        refusal.Message.ShouldContain("No terminal is hosted");
        refusal.Message.ShouldNotContain("permission");
    }

    [Fact]
    public async Task AMemberOfTheProject_Should_HoldTheAttachPermission()
    {
        // The guard must not have quietly raised the bar to Admin: #304 grants run.attach to both
        // bundles, with its cost recorded. A check that refuses everybody is not a check that works,
        // and the habitat refusal above is what proves this one got past the permission gate.
        using var host = Host(ProjectRole.Member);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Open", _runId, 80, 24)
        );

        refusal.Message.ShouldNotContain("do not have permission");
    }

    sealed record Created(Guid Id, string Name);

    /// <summary>
    /// A second host over the same containers with the permission seam replaced, connected to the
    /// terminal hub rather than the log one.
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

            _factory.CreateClient().Dispose();
        }

        public HubConnection Connect() =>
            new HubConnectionBuilder()
                .WithUrl(
                    new Uri(_factory.Server.BaseAddress, "hubs/run-terminal"),
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
