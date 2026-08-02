using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #222 — what the deployment tells its own portal. The two capabilities are asserted
/// **independently of each other**, because they coincide in the habitats we run today and a test
/// that only ever saw them together would not notice one being derived from the other.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class DeploymentCapabilities_Should_Constraint(ProjectsApiFixture fixture)
{
    sealed record Capabilities(bool HasCodeSource, bool CanStoreSecret, string? StoreRemedy);

    static async Task<Capabilities> Read(HttpClient client) =>
        (await client.GetFromJsonAsync<Capabilities>("/api/capabilities"))!;

    HttpClient Habitat(string? mode, ISecretStore? store = null) =>
        fixture
            .WithWebHostBuilder(builder =>
            {
                if (mode is not null)
                {
                    builder.UseSetting("Identity:Mode", mode);
                }

                if (store is not null)
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<ISecretStore>();
                        services.AddSingleton(store);
                    });
                }
            })
            .CreateClient();

    [Fact]
    public async Task ASelfHostDeployment_Should_OfferTheCodeSourceSurface()
    {
        var capabilities = await Read(Habitat("LocalOwner"));

        capabilities.HasCodeSource.ShouldBeTrue();
    }

    [Fact]
    public async Task ACloudDeployment_Should_NotOfferTheCodeSourceSurface()
    {
        var capabilities = await Read(Habitat(mode: null));

        capabilities.HasCodeSource.ShouldBeFalse();
    }

    [Fact]
    public async Task ADeploymentWithNoWritableStore_Should_SayItCannotStoreAndName_TheRemedy()
    {
        var capabilities = await Read(
            Habitat(mode: null, new UnavailableSecretStore("Set Secrets:Directory to store here."))
        );

        capabilities.CanStoreSecret.ShouldBeFalse();
        // The remedy travels so a form can state how to gain the option, not merely that it is
        // missing — the store already knows the sentence.
        capabilities.StoreRemedy.ShouldBe("Set Secrets:Directory to store here.");
    }

    [Fact]
    public async Task ASelfHostDeploymentWithAStore_Should_StillBeAbleToStore()
    {
        // The point of the change (design D3): storing follows the store, not the posture. A
        // self-host deployment configured with one stores perfectly well, and deriving the
        // capability from the posture would take a working option away from it.
        var capabilities = await Read(Habitat("LocalOwner", new RecordingSecretStore()));

        capabilities.HasCodeSource.ShouldBeTrue();
        capabilities.CanStoreSecret.ShouldBeTrue();
        capabilities.StoreRemedy.ShouldBeNull();
    }

    [Fact]
    public async Task TheCapabilities_Should_CarryNoConfigurationValue()
    {
        // Capabilities, not configuration (design D2): no mode string, no vault URI — a client
        // that could re-derive the rules would eventually disagree with the API about them.
        var raw = await Habitat("LocalOwner").GetStringAsync("/api/capabilities");

        raw.ShouldNotContain("LocalOwner");
        raw.ShouldNotContain("vault");
    }

    sealed class RecordingSecretStore : ISecretStore
    {
        public Task Store(
            string secretName,
            string value,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
