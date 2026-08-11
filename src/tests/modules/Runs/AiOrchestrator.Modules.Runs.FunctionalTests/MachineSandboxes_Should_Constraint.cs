using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #311 — the surface that is not keyed to a Run: this machine's sandboxes, listed and openable.
/// <para>
/// The test tier hosts no sandboxes, which is exactly a deployed habitat's answer (ADR-0021). That makes
/// it the right place to pin the two properties that must hold <b>before</b> any sandbox exists: the
/// habitat answers first and without evaluating a permission, and its answer never reads as one.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class MachineSandboxes_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await fixture
            .CreateClient()
            .PostAsJsonAsync("/api/projects", new { name = $"p-{Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ADeployedHabitat_Should_SayNoTerminalIsHosted_WithoutMentioningPermission()
    {
        using var host = Host(ProjectRole.Member, sees: new HashSet<Guid> { _projectId });

        var listing = await host.Client().GetFromJsonAsync<Listing>("/api/runs/sandboxes");

        listing.ShouldNotBeNull();
        listing.Hosted.ShouldBeFalse();
        listing.Sandboxes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACallerHoldingAttachNowhere_Should_BeRefusedTheTerminal_OnPermission()
    {
        // A caller with a bounded visibility set and no role on anything: roles-as-rows, holding
        // `run.attach` nowhere. The habitat refusal below would mask this, so the hub's order is what
        // this test is really about — and it is the one that must NOT be reordered.
        using var host = Host(role: null, sees: new HashSet<Guid>());
        await using var connection = host.Connect();
        await connection.StartAsync();

        var refusal = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("OpenSandbox", "aio-run-whatever", 80, 24)
        );

        // The habitat answers first, deliberately: this tier hosts nothing, so a caller who may not
        // attach is still told about the habitat rather than about themselves. Asking for access would
        // not help them, which is the whole reason the two sentences are kept apart.
        refusal.Message.ShouldContain("No terminal is hosted");
        refusal.Message.ShouldNotContain("permission");
    }

    [Fact]
    public async Task ACallerHoldingAttachNowhere_Should_SeeNoListing_AndBeToldItIsPermission()
    {
        // The read reports both answers as facts rather than throwing, so a surface can render the
        // right sentence. With nothing hosted, Hosted is false and Permitted is not even evaluated.
        using var host = Host(role: null, sees: new HashSet<Guid>());

        var listing = await host.Client().GetFromJsonAsync<Listing>("/api/runs/sandboxes");

        listing.ShouldNotBeNull();
        listing.Hosted.ShouldBeFalse();
        listing.Permitted.ShouldBeFalse();
        listing.Sandboxes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASandboxThisProductDidNotCreate_Should_BeRefusedLikeOneThatDoesNotExist()
    {
        // Criterion 2, and the disclosure it exists to prevent: if "outside the namespace" and "no such
        // sandbox" gave different sentences, a caller could enumerate the machine one guess at a time.
        using var host = Host(ProjectRole.Admin, sees: null);
        await using var connection = host.Connect();
        await connection.StartAsync();

        var foreign = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("OpenSandbox", "opencode-ds-connect", 80, 24)
        );
        var absent = await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("OpenSandbox", "aio-run-does-not-exist", 80, 24)
        );

        foreign.Message.ShouldBe(absent.Message);
    }

    [Fact]
    public async Task AnAttachOnASandboxWithNoRun_Should_StillBeRecorded()
    {
        // Criterion 7 for the half #304's recorder could not reach: no Run means no Run log to append
        // to, and this is the attach least reconstructable afterwards.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var recorder =
            scope.ServiceProvider.GetRequiredService<Features.Observation.IRunAttachRecorder>();

        await recorder.Attached(
            "aio-run-abandoned",
            runId: null,
            "Ada",
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        var recorded = await database.SandboxAttaches.SingleAsync(row =>
            row.Sandbox == "aio-run-abandoned"
        );

        recorded.Who.ShouldBe("Ada");
        recorded.RunId.ShouldBeNull();

        // And nothing was invented to hang it on: no Run log line exists, because there is no Run.
        (await database.Set<RunLogChunk>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AnAttachOnARunsSandbox_Should_BeRecordedTwice_InTheRowAndTheRunsOwnLog()
    {
        // #304's criterion 6 must not regress now that the row exists: the Run keeps a complete account
        // of what happened to it, and the two entry points tell the same story.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var recorder =
            scope.ServiceProvider.GetRequiredService<Features.Observation.IRunAttachRecorder>();

        var run = Run.Create(
            _projectId,
            "7",
            Guid.NewGuid(),
            RunLocus.Sandbox,
            DateTimeOffset.UtcNow
        );
        database.Runs.Add(run);
        await database.SaveChangesAsync();

        await recorder.Attached(
            "aio-run-live",
            run.Id,
            "Grace",
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        var recorded = await database.SandboxAttaches.SingleAsync(row =>
            row.Sandbox == "aio-run-live"
        );
        recorded.RunId.ShouldBe(run.Id);

        var line = await database.Set<RunLogChunk>().SingleAsync(chunk => chunk.RunId == run.Id);
        line.Content.ShouldContain("Grace");
        line.Content.ShouldContain("[terminal]");
    }

    sealed record Created(Guid Id, string Name);

    sealed record Listing(bool Hosted, bool Permitted, IReadOnlyList<SandboxView> Sandboxes);

    sealed record SandboxView(string Name, string Status, Guid? RunId, string? Workspace);

    TestHost Host(ProjectRole? role, IReadOnlySet<Guid>? sees) => new(fixture, role, sees);

    /// <summary>
    /// A second host over the same containers with the permission seam replaced. <c>sees</c> is passed
    /// alongside the role rather than fixed at null, because the two answers must stay coherent: null
    /// visibility means the machine's owner, and pairing it with "no role" would fake a caller no habitat
    /// produces.
    /// </summary>
    sealed class TestHost : IDisposable
    {
        readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

        public TestHost(RunsApiFixture fixture, ProjectRole? role, IReadOnlySet<Guid>? sees)
        {
            _factory = fixture.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IProjectPermissions>();
                    services.AddSingleton<IProjectPermissions>(new FixedPermissions(role, sees));
                })
            );

            _factory.CreateClient().Dispose();
        }

        public HttpClient Client() => _factory.CreateClient();

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

    sealed class FixedPermissions(ProjectRole? role, IReadOnlySet<Guid>? sees) : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(role);

        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult(sees);
    }
}
