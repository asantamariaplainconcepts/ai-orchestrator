using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// UC-010. The endpoint is unauthenticated and triggers work, so the refusals matter more than
/// the happy path — and BR-015's "identical events" is only structural if the webhook runs the
/// reconciler rather than parsing the payload, which is what the mirror assertions show.
/// </summary>
[Collection(BacklogCollection.Name)]
public class WebhookIngest_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    const string Secret = "a-webhook-secret";

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();

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

        // The stub resolver returns "stub-token" for any name, so the Connector names a secret
        // whose value the test can sign with.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        await database.Database.ExecuteSqlAsync(
            $"""UPDATE backlog.connectors SET "WebhookSecretName" = {"hook"} WHERE "ProjectId" = {_projectId}"""
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    const string Body = """{"repository":{"name":"portal","owner":{"login":"acme"}}}""";

    static string Sign(string body, string secret) =>
        "sha256="
        + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))
        );

    Task<HttpResponseMessage> Post(string body, string? signature, string eventName = "issues")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/github")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-GitHub-Event", eventName);
        if (signature is not null)
        {
            request.Headers.Add("X-Hub-Signature-256", signature);
        }

        return _client.SendAsync(request);
    }

    async Task<int> MirroredStories()
    {
        var backlog = await _client.GetFromJsonAsync<BacklogResponse>(
            $"/api/projects/{_projectId}/backlog"
        );
        return backlog!.Stories.Count;
    }

    [Fact]
    public async Task ASignedWebhook_Should_ReconcileExactlyAsAPollWould()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["bug"], null));

        // The stub resolver answers "stub-token" for every name, so that is the secret.
        var response = await Post(Body, Sign(Body, "stub-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The mirror filled — and it filled through the reconciler, not from the payload, which
        // named no stories at all.
        (await MirroredStories()).ShouldBe(1);
        fixture.Vendor.FetchCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EveryRefusal_Should_LookTheSame()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));

        var unsigned = await Post(Body, signature: null);
        var wrong = await Post(Body, Sign(Body, "not-the-secret"));
        var unknownRepository = await Post(
            """{"repository":{"name":"other","owner":{"login":"someone"}}}""",
            Sign("""{"repository":{"name":"other","owner":{"login":"someone"}}}""", "stub-token")
        );

        // One answer for all three: distinguishing them tells an unauthenticated caller which
        // repositories this installation watches.
        unsigned.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownRepository.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // And none of them did any work.
        (await MirroredStories()).ShouldBe(0);
    }

    [Fact]
    public async Task AnUninterestingEvent_Should_SucceedWithoutWorking()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));

        // A ping is not an error — a vendor that receives errors stops delivering.
        var response = await Post(Body, Sign(Body, "stub-token"), eventName: "ping");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MirroredStories()).ShouldBe(0);
    }

    [Fact]
    public async Task PollingStillReconciles_WhenNoWebhookArrives()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));

        // The property that makes webhooks safe to lose: the baseline is untouched by them.
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        (await MirroredStories()).ShouldBe(1);
    }

    [Fact]
    public async Task NoResponse_Should_EverCarryTheSecret()
    {
        var refused = await Post(Body, Sign(Body, "wrong"));
        var accepted = await Post(Body, Sign(Body, "stub-token"));

        (await refused.Content.ReadAsStringAsync()).ShouldNotContain("stub-token");
        (await accepted.Content.ReadAsStringAsync()).ShouldNotContain("stub-token");

        var backlog = await _client.GetStringAsync($"/api/projects/{_projectId}/backlog");
        backlog.ShouldNotContain("stub-token");
    }

    sealed record StoryResponse(string VendorId);

    sealed record BacklogResponse(object? Connector, IReadOnlyList<StoryResponse> Stories);
}
