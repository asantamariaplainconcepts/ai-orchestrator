using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// Boots the real AppHost — the same composition `aspire start` runs — and drives it with a real
/// browser. A green build is not evidence that a flow works; only driving the app is.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    DistributedApplication? _app;
    IPlaywright? _playwright;
    IBrowser? _browser;

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
        await _app.StartAsync();

        await _app
            .ResourceNotifications.WaitForResourceHealthyAsync("server")
            .WaitAsync(TimeSpan.FromMinutes(5));

        ServerBaseUrl = _app.GetEndpoint("server", "http").ToString();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
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
