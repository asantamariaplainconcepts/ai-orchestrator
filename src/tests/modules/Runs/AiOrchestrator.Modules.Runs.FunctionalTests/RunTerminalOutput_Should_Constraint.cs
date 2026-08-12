using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Runs.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The half #304 and #311 never pinned: a terminal that OPENS, and whose bytes reach the caller.
/// Every other terminal test asserts a refusal, so the pump — the thing a working terminal is — had
/// no coverage at all, and a shell that opens and then says nothing looked exactly like success.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunTerminalOutput_Should_Constraint(RunsApiFixture fixture)
{
    [Fact]
    public async Task AShellThatSpeaks_Should_ReachTheCallerThatOpenedIt()
    {
        using var host = new TestHost(fixture);
        await using var connection = host.Connect();

        var arrived = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        connection.On<byte[]>("output", chunk => arrived.TrySetResult(chunk));

        await connection.StartAsync();
        await connection.InvokeAsync("OpenSandbox", "aio-run-fake", 80, 24);

        var received = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        System.Text.Encoding.UTF8.GetString(received).ShouldBe("hello$ ");
    }

    /// <summary>A host that hosts exactly one sandbox, whose shell says one thing and then ends.</summary>
    sealed class SpeakingHost : IRunTerminalHost
    {
        public bool Hosted => true;

        public IRunTerminal? Open(Guid runId, int columns, int rows) => new SpeakingTerminal();

        public Task<IReadOnlyList<LocalSandbox>> List(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LocalSandbox>>([
                new LocalSandbox("aio-run-fake", "running", null, "/tmp/workspace"),
            ]);

        public Task<IRunTerminal?> Open(
            string sandbox,
            int columns,
            int rows,
            CancellationToken cancellationToken
        ) => Task.FromResult<IRunTerminal?>(new SpeakingTerminal());
    }

    sealed class SpeakingTerminal : IRunTerminal
    {
        readonly byte[] _greeting = System.Text.Encoding.UTF8.GetBytes("hello$ ");
        bool _spoken;

        public int Read(byte[] buffer)
        {
            if (_spoken)
            {
                // A real shell blocks here until it has something; ending immediately would race the
                // assertion against the pump's own teardown.
                Thread.Sleep(Timeout.Infinite);
            }

            _spoken = true;
            _greeting.CopyTo(buffer, 0);
            return _greeting.Length;
        }

        public void Write(ReadOnlySpan<byte> data) { }

        public void Dispose() { }
    }

    sealed class TestHost : IDisposable
    {
        readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

        public TestHost(RunsApiFixture fixture)
        {
            _factory = fixture.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IProjectPermissions>();
                    services.AddSingleton<IProjectPermissions>(new OwnerPermissions());
                    services.RemoveAll<IRunTerminalHost>();
                    services.AddSingleton<IRunTerminalHost>(new SpeakingHost());
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

    sealed class OwnerPermissions : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectRole?>(ProjectRole.Admin);

        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(null);
    }
}
