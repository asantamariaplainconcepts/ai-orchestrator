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

    /// <summary>
    /// The running composition. Exposed so a test can assert on the composition itself — #50
    /// exists because a resource was mis-wired for four changes with nothing watching.
    /// </summary>
    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("The AppHost has not started.");

    /// <summary>
    /// The vendor, stubbed at the HTTP boundary. Tests arrange it before driving the page; the
    /// host runs its real GitHub connector against it, so no live token is ever needed.
    /// </summary>
    public GitHubStub GitHub { get; } = new();

    /// <summary>The secret name the host can resolve — its value is the stub's throwaway token.</summary>
    public const string SecretName = "e2e-github";

    public IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        GitHub.Start();

        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AiOrchestrator_AppHost>();

        // Nor may it inherit the dev loop's agent substrate. `appsettings.json` puts agents in a
        // per-Run sandbox so a developer needs no flag, and this host loads that file too. CI has
        // no sbx, no daemon and no Docker identity, so inheriting it would turn every Run red for
        // a reason nobody chose.
        builder.Configuration["Parameters:sandbox"] = "false";

        // Nor may the run inherit the developer's Postgres — neither the *container*, which the
        // AppHost keeps alive between `aspire run` sessions, nor the *data volume* it mounts.
        //
        // Both are declined on the resource itself. The lifetime used to be asked for through
        // `DcpPublisher:ContainerLifetime`, which did not actually stop the persistent container
        // being reused: consecutive local runs found the previous run's rows and failed with
        // Project.NameAlreadyTaken (observed 2026-08-07 on a clean checkout, 12 of 45 red). That
        // was invisible for as long as the AppHost generated a fresh Postgres password each run —
        // the second run simply could not authenticate, and the symptom was a hang. Persisting
        // that password fixed the hang and revealed what it had been hiding, so the fix belongs
        // here rather than in whatever made it visible.
        //
        // CI never saw either symptom: a fresh machine per job has no container to reuse. This is
        // entirely a defect of the developer's own loop, which is where a test tier is read most.
        var postgres = builder
            .Resources.OfType<ContainerResource>()
            .Single(resource => resource.Name == "postgres");
        builder.CreateResourceBuilder(postgres).WithLifetime(ContainerLifetime.Session);
        foreach (var mount in postgres.Annotations.OfType<ContainerMountAnnotation>().ToList())
        {
            postgres.Annotations.Remove(mount);
        }

        // Run the host in its own E2E environment, not Development. This is deliberate on two
        // counts: dev-convenience configuration must never leak into E2E, and it puts the journey
        // on the *production* serving path (static wwwroot + index.html fallback) rather than the
        // dev proxy — so what E2E proves is what ships.
        var server = builder
            .Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == "server");
        builder
            .CreateResourceBuilder(server)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E")
            // Point the real connector at the stub, and give the resolver one secret to find.
            // Both are ordinary configuration — no test-only branch exists in the application.
            .WithEnvironment("Backlog__GitHub__BaseAddress", GitHub.BaseAddress.ToString())
            .WithEnvironment($"Secrets__{SecretName}", "stub-token")
            // The background poller would race the tests' explicit refresh for the same rows;
            // the deterministic path is the one worth asserting on.
            .WithEnvironment("Backlog__PollingEnabled", "false");

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

        try
        {
            await _app
                .ResourceNotifications.WaitForResourceHealthyAsync("server")
                .WaitAsync(TimeSpan.FromMinutes(3));
        }
        catch (TimeoutException)
        {
            // Without this the whole suite reads as "hung" and says nothing about why. The host's
            // own log is where the cause is — a failed migration, an unreachable dependency.
            throw new InvalidOperationException(
                "The server never became healthy.\n\n" + ServerLogTail(lines: 120)
            );
        }

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

        await GitHub.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "AppHost";
}
