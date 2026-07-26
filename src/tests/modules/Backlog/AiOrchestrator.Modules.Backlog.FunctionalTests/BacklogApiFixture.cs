using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using ErrorOr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// The Backlog module against real containers, with the <b>vendor</b> stubbed at the
/// <see cref="IBacklogConnector"/> seam.
/// <para>
/// Stubbing at the seam rather than at HTTP is deliberate: the tier stays hermetic — no network,
/// no GitHub token, no rate limit — while still exercising the real handlers, the real
/// reconciliation, and the real database. The GitHub implementation's own behaviour is covered by
/// unit tests over its error translation.
/// </para>
/// <para>
/// The background poller is switched off here. A timer firing mid-assertion is a flake generator,
/// and the refresh endpoint drives exactly the same synchroniser.
/// </para>
/// </summary>
public sealed class BacklogApiFixture : ApiServiceFixtureBase
{
    internal StubBacklogConnector Vendor { get; } = new();

    protected override string[] SchemasToReset => [BacklogDbContext.Schema];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Backlog:PollingEnabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBacklogConnector>();
            services.AddSingleton<IBacklogConnector>(Vendor);
            services.AddSingleton<ISecretResolver>(new StubSecretResolver());
        });
    }
}

/// <summary>A vendor whose responses the test decides, including how it fails.</summary>
sealed class StubBacklogConnector : IBacklogConnector
{
    public BacklogVendor Vendor => BacklogVendor.GitHub;

    public List<VendorStory> Stories { get; } = [];

    public Error? VerifyError { get; set; }

    public Error? FetchError { get; set; }

    public int FetchCount => _fetches;

    public void Reset()
    {
        Stories.Clear();
        VerifyError = null;
        FetchError = null;
        Interlocked.Exchange(ref _fetches, 0);
    }

    public Task<ErrorOr<Success>> VerifyAccess(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            VerifyError is { } error ? ErrorOrFactory.From<Success>([error]) : Result.Success
        );

    public Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _fetches);

        return Task.FromResult(
            FetchError is { } error
                ? ErrorOrFactory.From<BacklogSnapshot>([error])
                : new BacklogSnapshot([.. Stories])
        );
    }

    int _fetches;
}

sealed class StubSecretResolver : ISecretResolver
{
    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default) =>
        secretName == "missing-secret"
            ? throw new SecretNotFoundException(secretName)
            : Task.FromResult("stub-token");
}

[CollectionDefinition(Name)]
public sealed class BacklogCollection : ICollectionFixture<BacklogApiFixture>
{
    public const string Name = "Backlog";
}
