using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Where a conversation's agent pass runs, per habitat (#166, design D3).
/// <para>
/// Composed on the presence of the session host's configuration and never inferred from anything
/// else (ADR-0010): a pool endpoint means sessions, its absence means this process. The absence is
/// not a degraded mode — it is the correct answer on a machine one person owns and in DEC-049's
/// self-host deployment, neither of which has a session host to call.
/// </para>
/// </summary>
public static class ConversationRuntimeComposition
{
    /// <summary>
    /// The session pool's management endpoint. Its presence is the switch, like every other habitat
    /// decision here.
    /// </summary>
    public const string SessionPoolEndpointKey = "Conversations:SessionPoolEndpoint";

    /// <summary>Which credential the in-process runtime asks the agent runtime for.</summary>
    public const string RuntimeNameKey = "Conversations:Runtime";

    public static TBuilder AddConversationRuntime<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var endpoint = builder.Configuration[SessionPoolEndpointKey];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            builder.Services.AddSingleton<IConversationRuntime, InProcessConversationRuntime>();
            return builder;
        }

        // The portal's own identity, never a project credential: it is authorising itself to the
        // pool, and what happens inside the session uses the project's PAT resolved on that side.
        builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
        builder.Services.AddHttpClient<IConversationRuntime, SessionPoolConversationRuntime>(
            client => client.BaseAddress = new Uri(endpoint)
        );

        return builder;
    }
}

/// <summary>
/// One pass, here, in this process. For the habitats with no session host — where the same process
/// already resolves credentials and clones repositories, so nothing is being newly exposed.
/// </summary>
sealed class InProcessConversationRuntime(
    IAgentRuntimeSelector runtimes,
    ISecretResolver secrets,
    ICodeWorkspace workspace,
    IConfiguration configuration,
    ILogger<InProcessConversationRuntime> logger
) : IConversationRuntime
{
    public async Task<ConversationReply> Answer(
        Guid conversationId,
        ConversationContext context,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var runtimeName =
            configuration[ConversationRuntimeComposition.RuntimeNameKey] ?? "ClaudeCodeHeadless";
        var selection = runtimes.For(runtimeName);

        if (selection is null)
        {
            return new ConversationReply(
                false,
                $"No agent runtime named '{runtimeName}' is registered in this deployment.",
                null
            );
        }

        try
        {
            // Resolved by name at the last moment, exactly as a Run's is (BR-010): nothing secret
            // outlives this call, and the name is all that was ever stored.
            var apiKey = selection.CredentialSecretName is null
                ? string.Empty
                : await secrets.Resolve(selection.CredentialSecretName, cancellationToken);

            // Empty on the host path (DEC-069): no secret was named because the machine's own
            // tooling holds the identity, and exporting an empty vendor token is deliberately what
            // lets that tooling be used — an exported *value* would shadow it
            // (AgentCredentialEnvironment.For).
            var projectToken = context.SecretName is null
                ? string.Empty
                : await secrets.Resolve(context.SecretName, cancellationToken);

            // A workspace per pass here, deliberately: this habitat has one process and no session
            // to keep warm, so caching one would be a lifetime to manage for no latency anybody
            // notices locally. The session host is where warmth is worth paying for (design D2).
            //
            // The conversation's id stands in for a Run's in the branch name. Nothing is ever
            // published from it — a conversation writes nowhere (design D1) — so the branch is a
            // checkout detail, not a claim that work happened.
            var prepared = await workspace.Prepare(
                context.Code,
                conversationId,
                projectToken,
                cancellationToken
            );

            if (prepared.IsError)
            {
                return new ConversationReply(false, prepared.FirstError.Description, null);
            }

            var result = await selection.Runtime.Execute(
                new AgentInstruction(
                    Prompt: Prompt(context, message),
                    Action: "Conversation",
                    Timeout: TimeSpan.FromMinutes(5),
                    WorkspacePath: prepared.Value.Path,
                    Credentials: new AgentCredentials(projectToken, apiKey)
                ),
                cancellationToken
            );

            return new ConversationReply(result.Succeeded, result.Log, result.Usage);
        }
        catch (SecretNotFoundException exception)
        {
            // A named credential this deployment does not hold is a configuration problem the person
            // asking can act on, so it is the answer rather than a stack trace in a log.
            ConversationLog.CredentialMissing(logger, conversationId, exception.Message);
            return new ConversationReply(false, exception.Message, null);
        }
    }

    /// <summary>
    /// What the agent is asked. The Story's context when there is one, the project's repository
    /// either way — grounded in the code, not only in the mirror (#166).
    /// </summary>
    static string Prompt(ConversationContext context, string message) =>
        context.StoryContext is null
            ? $"You are answering a question about this repository.\n\nQuestion:\n{message}"
            : $"You are answering a question about this repository and this story.\n\n"
                + $"Story:\n{context.StoryContext}\n\nQuestion:\n{message}";
}

/// <summary>
/// One pass, in a container of this conversation's own (design D2).
/// <para>
/// The conversation id is the session identifier, so the same conversation reaches the same warm
/// container with its workspace already cloned, and the host reclaims it after inactivity. One
/// conversation, one container, one project's credential: the isolation boundary coincides with the
/// credential boundary (DEC-030), and this process never holds a project's PAT.
/// </para>
/// </summary>
sealed class SessionPoolConversationRuntime(
    HttpClient client,
    TokenCredential credential,
    ILogger<SessionPoolConversationRuntime> logger
) : IConversationRuntime
{
    static readonly string[] Scope = ["https://dynamicsessions.io/.default"];

    public async Task<ConversationReply> Answer(
        Guid conversationId,
        ConversationContext context,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(Scope),
                cancellationToken
            );

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                // The identifier is what makes the container this conversation's. Same id, same
                // container, while the host still holds it.
                $"?api-version=2024-02-02-preview&identifier={conversationId}"
            )
            {
                Content = JsonContent.Create(
                    new
                    {
                        message,
                        // Names, never values: the session resolves the credential on its own side,
                        // with its own identity (BR-010).
                        secretName = context.SecretName,
                        owner = context.Code.Owner,
                        repository = context.Code.Repository,
                        storyContext = context.StoryContext,
                    }
                ),
            };
            request.Headers.Authorization = new("Bearer", token.Token);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The host's own words, truncated: a 500 from a session says something a person can
                // act on more often than "the pass failed" does.
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                ConversationLog.SessionRefused(logger, conversationId, (int)response.StatusCode);
                return new ConversationReply(
                    false,
                    $"The conversation host answered {(int)response.StatusCode}: {Truncate(body)}",
                    null
                );
            }

            var reply = await response.Content.ReadFromJsonAsync<SessionReply>(cancellationToken);

            return reply is null
                ? new ConversationReply(false, "The conversation host answered nothing.", null)
                : new ConversationReply(
                    reply.Succeeded,
                    reply.Body,
                    // Absent stays absent: a session that reported no usage must not read as free
                    // (BR-011).
                    reply.InputTokens
                        is { } input
                    && reply.OutputTokens is { } output
                    && reply.CostUsd is { } cost
                        ? new AgentUsage(input, output, cost)
                        : null
                );
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException)
        {
            ConversationLog.SessionUnreachable(logger, exception, conversationId);
            return new ConversationReply(
                false,
                "The conversation host could not be reached. The message was not answered; try again.",
                null
            );
        }
    }

    static string Truncate(string value) => value.Length <= 500 ? value : value[..500];

    sealed record SessionReply(
        bool Succeeded,
        string Body,
        long? InputTokens,
        long? OutputTokens,
        decimal? CostUsd
    );
}

static partial class ConversationLog
{
    [LoggerMessage(
        EventId = 7100,
        Level = LogLevel.Warning,
        Message = "Conversation {ConversationId} could not resolve a credential: {Reason}"
    )]
    public static partial void CredentialMissing(
        ILogger logger,
        Guid conversationId,
        string reason
    );

    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Warning,
        Message = "The session host refused conversation {ConversationId} with {StatusCode}"
    )]
    public static partial void SessionRefused(ILogger logger, Guid conversationId, int statusCode);

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Error,
        Message = "The session host was unreachable for conversation {ConversationId}"
    )]
    public static partial void SessionUnreachable(
        ILogger logger,
        Exception exception,
        Guid conversationId
    );
}
