using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// The half of automation-defaults that reaches the vendor. Tested here rather than beside the
/// Automations, because this is where the connector is stubbed — and the claim under test is
/// precisely about what arrives at the vendor and what does not.
/// </summary>
[Collection(BacklogCollection.Name)]
public class DefaultLabels_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<DefaultsResponse> ApplyDefaults()
    {
        var response = await _client.PostAsync(
            $"/api/projects/{_projectId}/automations/defaults",
            content: null
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DefaultsResponse>())!;
    }

    [Fact]
    public async Task TheTriggerLabels_Should_ExistInTheRepositoryWithoutTouchingAStory()
    {
        fixture.Vendor.Stories.Add(new Connectors.VendorStory("1", "A story", "open", [], null));

        var result = await ApplyDefaults();

        result.LabelNote.ShouldBeNull();
        fixture
            .Vendor.RepositoryLabels.OrderBy(label => label)
            .ShouldBe(["ai:estimate", "ai:implement", "ai:refine", "ai:transition"]);

        // The point of the whole exercise: a Member can choose these at the vendor *before*
        // anybody has labelled anything, so no Story may have been modified to make them exist.
        fixture.Vendor.Stories.Single().Labels.ShouldBeEmpty();
    }

    [Fact]
    public async Task AVendorRefusal_Should_LeaveTheAutomationsCreatedAndNameTheLabels()
    {
        fixture.Vendor.EnsureLabelError = BacklogErrors.VendorUnavailable("rate limited");

        var result = await ApplyDefaults();

        // Automations first, labels second (design D4): an outage cannot leave the project with
        // nothing, having skipped the part that needed no vendor at all.
        result.Created.Count.ShouldBe(4);
        result.LabelNote.ShouldNotBeNull();
        result.LabelNote.ShouldContain("ai:implement");
        fixture.Vendor.RepositoryLabels.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnsuringLabelsTwice_Should_BeAsQuietAsEnsuringThemOnce()
    {
        await ApplyDefaults();

        var again = await ApplyDefaults();

        again.LabelNote.ShouldBeNull();
        fixture.Vendor.RepositoryLabels.Count.ShouldBe(4);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id, string TriggerLabel);

    sealed record SkippedResponse(string TriggerLabel, string Reason);

    sealed record DefaultsResponse(
        IReadOnlyList<AutomationResponse> Created,
        IReadOnlyList<SkippedResponse> Skipped,
        string? LabelNote
    );
}
