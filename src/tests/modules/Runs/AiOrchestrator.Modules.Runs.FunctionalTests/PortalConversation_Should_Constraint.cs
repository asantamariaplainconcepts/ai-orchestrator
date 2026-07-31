using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #166 — a conversation is not a Run, one message is one pass, and a failed pass does not end it.
/// <para>
/// The first of those is the one worth stating twice: everything else in this module treats "there
/// is work on this Story" as a Run occupying it, and the whole point of this capability is that a
/// person talking about a Story does not stop that Story's Automations.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class PortalConversation_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Conversations.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        // A conversation is grounded in the project's code, so it needs a Connector like a Run does.
        var connected = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
            }
        );
        connected.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Start(string? vendorStoryId = null) =>
        _client.PostAsJsonAsync($"/api/projects/{_projectId}/conversations", new { vendorStoryId });

    async Task<Guid> StartedConversation(string? vendorStoryId = null)
    {
        var response = await Start(vendorStoryId);
        response.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync()
        );
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;
    }

    /// <summary>
    /// The body rides every failure. A bare EnsureSuccessStatusCode turns a server-side exception
    /// into "500", which is the least useful sentence a failing test can print — and outside
    /// Production this host puts the actual exception in the body.
    /// </summary>
    async Task<HttpResponseMessage> Say(Guid conversationId, string body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/conversations/{conversationId}/messages",
            new { body }
        );

        if ((int)response.StatusCode >= 500)
        {
            throw new InvalidOperationException(
                $"Saying '{body}' failed: {await response.Content.ReadAsStringAsync()}"
            );
        }

        return response;
    }

    [Fact]
    public async Task AConversation_Should_ExistWithoutARun()
    {
        var conversationId = await StartedConversation("42");

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

        // The assertion the whole design rests on: no Run row exists for this project at all, so
        // nothing occupies the Story and nothing counts against BR-002's cap.
        (await database.Runs.AnyAsync(run => run.ProjectId == _projectId)).ShouldBeFalse();
        (await database.Conversations.CountAsync(c => c.ProjectId == _projectId)).ShouldBe(1);
        _ = conversationId;
    }

    [Fact]
    public async Task AConversationAboutNothing_Should_BeAnOrdinaryCase()
    {
        // Not a degraded conversation: "what would you do here" is a question about a project, and
        // forcing a subject would make the commonest question unaskable.
        var conversationId = await StartedConversation(vendorStoryId: null);

        (await Say(conversationId, "What would you do here?")).EnsureSuccessStatusCode();

        fixture.Conversations.Contexts.ShouldHaveSingleItem().StoryContext.ShouldBeNull();
    }

    [Fact]
    public async Task OneMessage_Should_CostExactlyOnePass()
    {
        var conversationId = await StartedConversation();

        (await Say(conversationId, "Why did this fail?")).EnsureSuccessStatusCode();

        // ADR-0008's model, counted rather than assumed: one message, one pass.
        fixture.Conversations.Passes.ShouldBe(1);
    }

    [Fact]
    public async Task APassesUsage_Should_BeRecordedAgainstTheConversation()
    {
        var conversationId = await StartedConversation();

        var response = await Say(conversationId, "Why did this fail?");
        var conversation = await response.Content.ReadFromJsonAsync<ConversationResponse>();

        conversation!.SpendUsd.ShouldBe(0.004m);
        conversation.SpendIsComplete.ShouldBeTrue();

        var answer = conversation.Messages.Last();
        answer.Role.ShouldBe("Agent");
        answer.InputTokens.ShouldBe(120);
        answer.CostUsd.ShouldBe(0.004m);
    }

    [Fact]
    public async Task AnUnmeasuredPass_Should_ReadUnknownAndNotClaimAnExactTotal()
    {
        var conversationId = await StartedConversation();
        (await Say(conversationId, "First, measured.")).EnsureSuccessStatusCode();

        // BR-011: a runtime that reported nothing is unknown, never zero.
        fixture.Conversations.Next = new ConversationReply(true, "No usage reported.", null);
        var response = await Say(conversationId, "Second, unmeasured.");
        var conversation = await response.Content.ReadFromJsonAsync<ConversationResponse>();

        conversation!.Messages.Last().CostUsd.ShouldBeNull();

        // The total still holds what was measured — and says it is a floor rather than a fact.
        conversation.SpendUsd.ShouldBe(0.004m);
        conversation.SpendIsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task AFailedPass_Should_LeaveTheConversationOpen()
    {
        var conversationId = await StartedConversation();

        fixture.Conversations.Next = new ConversationReply(false, "The model timed out.", null);
        var failed = await Say(conversationId, "Why did this fail?");

        // A failure is a message, not an error out of the endpoint: the exchange is the answer, and
        // it now contains something the person can read.
        failed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterFailure = await failed.Content.ReadFromJsonAsync<ConversationResponse>();
        afterFailure!.Messages.Last().Failed.ShouldBeTrue();

        // And the next message still works, which is what "stays open" has to mean.
        fixture.Conversations.Next = new ConversationReply(true, "Recovered.", null);
        var second = await Say(conversationId, "Try again?");
        second.EnsureSuccessStatusCode();
        (await second.Content.ReadFromJsonAsync<ConversationResponse>())!
            .Messages.Last()
            .Failed.ShouldBeFalse();
    }

    [Fact]
    public async Task AStoryConversation_Should_BeGivenTheStoryAndWriteNothingToTheVendor()
    {
        fixture.Vendor.Stories.Add(
            new VendorStory("42", "A story with a title", "open", [], "The body of it")
        );
        (
            await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null)
        ).EnsureSuccessStatusCode();

        var conversationId = await StartedConversation("42");
        (await Say(conversationId, "What is this about?")).EnsureSuccessStatusCode();

        // Given the Story, read from the mirror (BR-008).
        var context = fixture.Conversations.Contexts.ShouldHaveSingleItem();
        context.StoryContext.ShouldNotBeNull();
        context.StoryContext.ShouldContain("A story with a title");

        // And the vendor untouched: a conversation leaves no trace on the Story (design D1). This is
        // the assertion that would catch somebody "helpfully" mirroring the exchange as a comment.
        fixture.Vendor.Comments.ShouldBeEmpty();
    }

    [Fact]
    public async Task AConversationIsGroundedInTheCode_Should_CarryTheProjectsCredentialByName()
    {
        var conversationId = await StartedConversation();
        (await Say(conversationId, "Where is the entry point?")).EnsureSuccessStatusCode();

        var context = fixture.Conversations.Contexts.ShouldHaveSingleItem();
        context.Code.Owner.ShouldBe("acme");
        context.Code.Repository.ShouldBe("portal");

        // The name, never the value (BR-010): the portal hands over what to resolve, and the far
        // side of the seam resolves it.
        context.SecretName.ShouldNotBeNullOrWhiteSpace();
        context.SecretName.ShouldNotContain("github_pat");
    }

    [Fact]
    public async Task AProjectWithNoConnector_Should_SayWhyRatherThanAnswerWorse()
    {
        var bare = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"bare-{Guid.NewGuid():N}" }
        );
        var bareId = (await bare.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        var started = await _client.PostAsJsonAsync(
            $"/api/projects/{bareId}/conversations",
            new { vendorStoryId = (string?)null }
        );
        var conversationId = (await started.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{bareId}/conversations/{conversationId}/messages",
            new { body = "Anything?" }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("no Connector");
        fixture.Conversations.Passes.ShouldBe(0);
    }

    [Fact]
    public async Task AConversationOfAnotherProject_Should_NotBeReachableByGuessingItsId()
    {
        var conversationId = await StartedConversation();

        var other = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"other-{Guid.NewGuid():N}" }
        );
        var otherId = (await other.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        // The lookup is scoped to the project in the route. Without that, holding the permission on
        // any project would be holding it on every conversation whose id you could guess.
        var response = await _client.GetAsync(
            $"/api/projects/{otherId}/conversations/{conversationId}"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record ConversationResponse(
        Guid Id,
        Guid ProjectId,
        string? VendorStoryId,
        decimal SpendUsd,
        bool SpendIsComplete,
        List<MessageResponse> Messages
    );

    sealed record MessageResponse(
        Guid Id,
        string Role,
        string Body,
        bool Failed,
        long? InputTokens,
        long? OutputTokens,
        decimal? CostUsd
    );
}
