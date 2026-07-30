using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #160 — editing a Connector without re-pasting the credential it already stores. What must hold:
/// absent means "keep it" only when there is something to keep, the kept credential is still
/// re-verified before anything saves, and none of the old refusals loosen on the way.
/// </summary>
[Collection(BacklogCollection.Name)]
public class KeptCredential_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    const string Token = "github_pat_11ABCDE_thisisthesecretvalue";

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Secrets.Reset();
        fixture.Caller.Reset();
        fixture.Permissions.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Configure(object body) =>
        _client.PutAsJsonAsync($"/api/projects/{_projectId}/connector", body);

    /// <summary>A first connect by pasting, which is what leaves a credential to be kept.</summary>
    async Task Connect() =>
        (
            await Configure(
                new
                {
                    owner = "acme",
                    repository = "portal",
                    accessToken = Token,
                }
            )
        ).EnsureSuccessStatusCode();

    async Task<(string SecretName, string? PromptDirectory, string Repository)> Stored()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        var connector = await database.Connectors.SingleAsync(entity =>
            entity.ProjectId == _projectId
        );
        return (connector.SecretName, connector.PromptDirectory, connector.Repository);
    }

    [Fact]
    public async Task EditingASetting_Should_KeepTheStoredCredentialAndReverifyIt()
    {
        await Connect();
        var before = await Stored();

        // The case #150 exposed: a setting to change and no token in hand.
        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                promptDirectory = "prompts/ours",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var after = await Stored();
        after.PromptDirectory.ShouldBe("prompts/ours");
        after.SecretName.ShouldBe(before.SecretName);

        // Resolved from the store rather than from the request — the probe saw the stored value.
        fixture.Vendor.VerifiedToken.ShouldBe(Token);
    }

    [Fact]
    public async Task TheKeptCredential_Should_NotComeBackOutOrBeRestored()
    {
        await Connect();
        var storedBefore = fixture.Secrets.Stored.Count;

        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                promptDirectory = "prompts/ours",
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(Token);

        // Nothing new to store — BR-010's value stays exactly where it was put, under one name.
        fixture.Secrets.Stored.Count.ShouldBe(storedBefore);
    }

    [Fact]
    public async Task WithNoConnector_Should_StillRefuseNeither()
    {
        var response = await Configure(new { owner = "acme", repository = "portal" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("no Connector");
    }

    [Fact]
    public async Task BothCredentials_Should_StillBeRefused()
    {
        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                accessToken = Token,
                secretName = "acme-pat",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("not both");
    }

    [Fact]
    public async Task AVendorSwitch_Should_BeRefusedNamingWhy()
    {
        await Connect();

        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                vendor = "AzureDevOps",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("GitHub");
        body.ShouldContain("AzureDevOps");
    }

    [Fact]
    public async Task AnEditTheCredentialCannotServe_Should_RefuseAndSaveNothing()
    {
        await Connect();
        // The refusal is what proves the reuse path probes at all: it can only come from the probe.
        fixture.Vendor.StoriesRefusal = Error.Failure(
            "Vendor.NoAccess",
            "the token cannot read acme/other"
        );

        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "other",
                promptDirectory = "prompts/ours",
            }
        );

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await response.Content.ReadAsStringAsync()).ShouldContain("cannot read acme/other");

        // Nothing saved: the refusal is not a partial write.
        var after = await Stored();
        after.Repository.ShouldBe("portal");
        after.PromptDirectory.ShouldBeNull();
    }

    [Fact]
    public async Task ANonAdmin_Should_NotEditBehindAStoredCredential()
    {
        await Connect();
        fixture.Permissions.Role = ProjectRole.Member;

        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                promptDirectory = "prompts/ours",
            }
        );

        // Reuse stores nothing, so #119's check would not have fired — and configuration would have
        // become editable by a caller who may not paste a token.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Stored()).PromptDirectory.ShouldBeNull();
    }

    [Fact]
    public async Task Rotation_Should_StillReplaceTheStoredValue()
    {
        await Connect();

        (
            await Configure(
                new
                {
                    owner = "acme",
                    repository = "portal",
                    accessToken = "github_pat_11ABCDE_arotatedvalue",
                }
            )
        ).EnsureSuccessStatusCode();

        fixture.Vendor.VerifiedToken.ShouldBe("github_pat_11ABCDE_arotatedvalue");
    }
}
