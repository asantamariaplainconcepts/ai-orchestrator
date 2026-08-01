using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #215 — the picker's listing: live names when the Connector can read, and degradation as data
/// (a reason, never a 500) when it cannot. The save path never learns about any of this, which is
/// why these tests only ever GET.
/// </summary>
[Collection(BacklogCollection.Name)]
public class ProjectPrompts_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Configure() =>
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

    async Task<PromptsResponse> Read() =>
        (await _client.GetFromJsonAsync<PromptsResponse>($"/api/projects/{_projectId}/prompts"))!;

    [Fact]
    public async Task Prompts_Should_ListTheDirectorysMarkdownFiles()
    {
        await Configure();
        fixture.Vendor.DirectoryFiles = ["estimate.md", "triage.md", "notes.txt"];

        var prompts = await Read();

        prompts.Reason.ShouldBeNull();
        prompts.Directory.ShouldBe("ai/prompts");
        prompts.Names.ShouldBe(["estimate.md", "triage.md"]);
    }

    [Fact]
    public async Task Prompts_Should_ReadAnAbsentDirectoryAsNothingThereYet()
    {
        await Configure();
        fixture.Vendor.DirectoryFiles = null;

        var prompts = await Read();

        // Absent and empty are the same honest answer — nothing to offer, no failure (#215).
        prompts.Reason.ShouldBeNull();
        prompts.Names.ShouldBeEmpty();
    }

    [Fact]
    public async Task Prompts_Should_DegradeWithAReasonWhenThereIsNoConnector()
    {
        var prompts = await Read();

        prompts.Reason.ShouldNotBeNull();
        prompts.Names.ShouldBeEmpty();
        // The default directory still travels, so the form can name where prompts would live.
        prompts.Directory.ShouldBe("ai/prompts");
    }

    [Fact]
    public async Task Prompts_Should_DegradeWithTheVendorsReasonWhenTheListingIsRefused()
    {
        await Configure();
        fixture.Vendor.ListDirectoryError = BacklogErrors.CredentialRejected("acme-pat");

        var prompts = await Read();

        prompts.Reason.ShouldNotBeNull();
        prompts.Names.ShouldBeEmpty();
    }

    sealed record PromptsResponse(string Directory, List<string> Names, string? Reason);
}
