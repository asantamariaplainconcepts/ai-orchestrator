using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// Boots the real AppHost — the same composition `aspire start` runs — and drives it with a real
/// browser. A green build is not evidence that a flow works; only driving the app is.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    readonly List<string> _serverLogs = [];

    DistributedApplication? _app;
    IPlaywright? _playwright;
    IBrowser? _browser;
    CancellationTokenSource? _logWatch;

    public string ServerBaseUrl { get; private set; } = string.Empty;

    public IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AiOrchestrator_AppHost>();

        // Containers must not outlive the run: a persistent lifetime leaks state between runs,
        // which is a recurring source of "passes locally, fails in CI" defects.
        builder.Configuration["DcpPublisher:ContainerLifetime"] = "Session";

        // Run the host in its own E2E environment, not Development. This is deliberate on two
        // counts: dev-convenience configuration must never leak into E2E, and it puts the journey
        // on the *production* serving path (static wwwroot + index.html fallback) rather than the
        // dev proxy — so what E2E proves is what ships.
        var server = builder
            .Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == "server");
        builder.CreateResourceBuilder(server).WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E");

        _app = await builder.BuildAsync();

        // Without the host's own logs, an E2E failure tells you a status code and nothing about
        // why — the ProblemDetails body deliberately says nothing. Two hard-won details here:
        // the watch keys on the resource's runtime ResourceId (not its declared name — watching
        // "server" by name yielded an empty stream twice), resolved from the notification
        // events; and it starts before StartAsync so the startup backlog is captured.
        _logWatch = new CancellationTokenSource();
        var loggers = _app.Services.GetRequiredService<ResourceLoggerService>();
        var notifications = _app.ResourceNotifications;
        _ = Task.Run(
            async () =>
            {
                var watched = new HashSet<string>();
                await foreach (
                    var resourceEvent in notifications
                        .WatchAsync(_logWatch.Token)
                        .WithCancellation(_logWatch.Token)
                )
                {
                    if (resourceEvent.Resource.Name != "server")
                    {
                        continue;
                    }

                    if (!watched.Add(resourceEvent.ResourceId))
                    {
                        continue;
                    }

                    var resourceId = resourceEvent.ResourceId;
                    _ = Task.Run(
                        async () =>
                        {
                            await foreach (
                                var batch in loggers
                                    .WatchAsync(resourceId)
                                    .WithCancellation(_logWatch.Token)
                            )
                            {
                                foreach (var line in batch)
                                {
                                    lock (_serverLogs)
                                    {
                                        _serverLogs.Add(line.Content);
                                    }
                                }
                            }
                        },
                        _logWatch.Token
                    );
                }
            },
            _logWatch.Token
        );

        await _app.StartAsync();

        await _app
            .ResourceNotifications.WaitForResourceHealthyAsync("server")
            .WaitAsync(TimeSpan.FromMinutes(5));

        ServerBaseUrl = _app.GetEndpoint("server", "http").ToString();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    /// <summary>The tail of the host's console output, for failure messages.</summary>
    public string ServerLogTail(int lines = 40)
    {
        lock (_serverLogs)
        {
            return _serverLogs.Count == 0
                ? "(no server logs captured — the log watch itself may be broken)"
                : string.Join(Environment.NewLine, _serverLogs.TakeLast(lines));
        }
    }

    public async Task DisposeAsync()
    {
        if (_logWatch is not null)
        {
            await _logWatch.CancelAsync();
            _logWatch.Dispose();
        }

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "AppHost";
}
