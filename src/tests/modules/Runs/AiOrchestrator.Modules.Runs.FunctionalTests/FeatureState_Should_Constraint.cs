using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #331 — the feature manager is composed from <c>IConfiguration</c> and from nothing else, and a
/// habitat that declares no features starts exactly as it did before it existed.
/// <para>
/// <b>This is the test for a seam with no consumer.</b> RULE-007 names that an anti-pattern and the
/// owner accepted it knowingly, so what is asserted here is deliberately narrow: it resolves, it
/// reads configuration, and it changes nothing. Anything more would be testing a feature nobody has
/// written yet.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class FeatureState_Should_Constraint(RunsApiFixture fixture)
{
    [Theory]
    // The habitats the test tiers cover, by the one key that separates them: the cloud posture
    // (nothing declared), the dev loop, and the compose self-host that declines Local folders.
    [InlineData(null, null)]
    [InlineData("LocalOwner", null)]
    [InlineData(
        "LocalOwner",
        "the orchestrator runs in a container here, and a folder on this machine is not visible to it"
    )]
    public async Task TheFeatureManager_Should_ResolveInEveryHabitat(
        string? identityMode,
        string? localFolderUnavailable
    )
    {
        using var host = fixture.WithWebHostBuilder(builder =>
        {
            if (identityMode is not null)
            {
                builder.UseSetting("Identity:Mode", identityMode);
            }
            if (localFolderUnavailable is not null)
            {
                builder.UseSetting("Habitat:LocalFolderUnavailableReason", localFolderUnavailable);
            }
        });

        // Creating the client is what actually starts the host — a composition failure surfaces
        // here rather than as a resolve error below.
        using var client = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var features = scope.ServiceProvider.GetRequiredService<IVariantFeatureManager>();

        // No FeatureManagement section is declared anywhere in these habitats: an undeclared
        // feature is off, and asking costs no external service — there is none to reach.
        (
            await features.IsEnabledAsync("nothing-declares-this", CancellationToken.None)
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task ADeclaredFeature_Should_BeReadableFromConfigurationAlone()
    {
        using var host = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:worktree-runs", "true")
        );
        using var client = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var features = scope.ServiceProvider.GetRequiredService<IVariantFeatureManager>();

        // Configuration is the whole source. No Azure App Configuration client, endpoint or
        // credential took part in this answer — DEC-049's stranger with Docker still runs it.
        (await features.IsEnabledAsync("worktree-runs", CancellationToken.None)).ShouldBeTrue();
        (await features.IsEnabledAsync("some-other-flag", CancellationToken.None)).ShouldBeFalse();
    }
}
