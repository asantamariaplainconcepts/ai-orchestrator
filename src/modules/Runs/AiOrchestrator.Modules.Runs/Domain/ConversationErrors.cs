using ErrorOr;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// What a conversation can refuse (#166). A closed set, like every other module's, so the API's
/// problem codes stay enumerable.
/// <para>
/// Note what is <b>not</b> here: nothing about a failed agent pass. A pass that fails becomes a
/// message inside the conversation, not an error out of it — the conversation stays open and takes
/// another, and returning a problem would tell the caller the wrong thing about what happened.
/// </para>
/// </summary>
static class ConversationErrors
{
    /// <summary>
    /// Also the answer for a conversation that belongs to another project, deliberately: the lookup
    /// is scoped to the project in the route, so "not yours" and "not there" are one refusal and
    /// neither confirms the other's id exists.
    /// </summary>
    public static Error NotFound(Guid conversationId) =>
        Error.NotFound(
            "Conversation.NotFound",
            $"Conversation '{conversationId}' was not found in this project."
        );

    /// <summary>
    /// A conversation is grounded in the project's code, and the Connector is what says where that
    /// is and which credential reaches it. Answering from the mirror alone would be a quietly worse
    /// answer with nothing saying so.
    /// </summary>
    public static Error NoConnector() =>
        Error.Validation(
            "Conversation.NoConnector",
            "This project has no Connector, so there is no repository to ground an answer in. "
                + "Configure one first."
        );
}
