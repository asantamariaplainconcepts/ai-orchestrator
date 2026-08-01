using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.ConversationSession;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Secrets;

// One conversation's container (#166, design D2). The session host starts it on the conversation's
// first message, keeps it while the conversation is being used, and reclaims it after inactivity —
// so the workspace below is cloned once and answers every later message warm.
//
// One conversation, one container, one project's credential: the isolation boundary coincides with
// the credential boundary (DEC-030), and the portal that called this holds neither.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Resolved here, with this container's own identity — never handed over by the caller. The portal
// sends the credential's NAME (BR-010); reading it is this side's job.
builder.AddSecretResolution();
builder.AddAgentRuntime();
builder.AddCodeWorkspace();
builder.AddLocalCodeWorkspace();

// The warm part. Singleton because the container IS the conversation: the first message pays the
// clone and every later one does not, which is the whole reason this shape was chosen over a job
// per message.
builder.Services.AddSingleton<WarmWorkspace>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapPost(
    "/",
    async (
        SessionRequest request,
        WarmWorkspace workspace,
        ISecretResolver secrets,
        IAgentRuntimeSelector runtimes,
        IConfiguration configuration,
        CancellationToken cancellationToken
    ) =>
    {
        var runtimeName = configuration["Conversations:Runtime"] ?? "ClaudeCodeHeadless";
        var selection = runtimes.For(runtimeName);

        if (selection is null)
        {
            return Results.Ok(
                SessionResponse.Failed($"No agent runtime named '{runtimeName}' is registered.")
            );
        }

        try
        {
            var projectToken = await secrets.Resolve(request.SecretName, cancellationToken);
            var apiKey = selection.CredentialSecretName is null
                ? string.Empty
                : await secrets.Resolve(selection.CredentialSecretName, cancellationToken);

            var path = await workspace.PathFor(
                new CodeCoordinates(request.Owner, request.Repository),
                projectToken,
                cancellationToken
            );

            if (path.IsError)
            {
                return Results.Ok(SessionResponse.Failed(path.FirstError.Description));
            }

            var result = await selection.Runtime.Execute(
                new AgentInstruction(
                    Prompt: Prompt(request),
                    Action: "Conversation",
                    Timeout: TimeSpan.FromMinutes(5),
                    WorkspacePath: path.Value,
                    Credentials: new AgentCredentials(projectToken, apiKey)
                ),
                cancellationToken
            );

            return Results.Ok(
                new SessionResponse(
                    result.Succeeded,
                    result.Log,
                    // Absent stays absent across the wire too: a pass the runtime did not measure
                    // must not arrive at the portal looking free (BR-011).
                    result.Usage?.InputTokens,
                    result.Usage?.OutputTokens,
                    result.Usage?.CostUsd
                )
            );
        }
        catch (SecretNotFoundException exception)
        {
            // A named credential this deployment does not hold is something the person asking can
            // act on, so it is the answer rather than a 500 the portal has to guess about.
            return Results.Ok(SessionResponse.Failed(exception.Message));
        }
    }
);

await app.RunAsync();

static string Prompt(SessionRequest request) =>
    request.StoryContext is null
        ? $"You are answering a question about this repository.\n\nQuestion:\n{request.Message}"
        : $"You are answering a question about this repository and this story.\n\n"
            + $"Story:\n{request.StoryContext}\n\nQuestion:\n{request.Message}";

/// <summary>What the portal sends. Names and coordinates — never a credential's value (BR-010).</summary>
sealed record SessionRequest(
    string Message,
    string SecretName,
    string Owner,
    string Repository,
    string? StoryContext
);

sealed record SessionResponse(
    bool Succeeded,
    string Body,
    long? InputTokens,
    long? OutputTokens,
    decimal? CostUsd
)
{
    public static SessionResponse Failed(string reason) => new(false, reason, null, null, null);
}
