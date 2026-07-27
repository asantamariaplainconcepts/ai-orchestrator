using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;

namespace AiOrchestrator.Modules.Runs.Features.Conversation;

/// <summary>
/// The two moves a conversational action makes: end a pass by asking, and read what came back.
/// <para>
/// No production action calls <see cref="AskAndWait"/> yet — the grill action (#79) is its first
/// consumer, deliberately a separate change (RULE-005). ARCHITECTURE.md carries that status per
/// ADR-0006 until it does.
/// </para>
/// </summary>
sealed class ConversationGate(IStoryWriter stories, IConversationReader conversation)
{
    /// <summary>
    /// Posts the questions to the Story signed with the Run's marker and puts the Run into its
    /// untimed wait. The caller saves; the container exits with the ordinary end of its pass.
    /// Returns null on success, otherwise why the questions could not be delivered — a Run must
    /// never wait on questions nobody can read.
    /// </summary>
    public async Task<string?> AskAndWait(
        Run run,
        string questions,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    )
    {
        var delivery = await stories.AddComment(
            run.ProjectId,
            run.VendorStoryId,
            RunMarker.Sign(run.Id, questions),
            cancellationToken
        );

        if (delivery is not null)
        {
            return delivery;
        }

        run.AwaitInput(at);
        return null;
    }

    /// <summary>
    /// The conversation since the Run started waiting: every human comment, agent comments
    /// excluded by marker. <see cref="ConversationResult.Failure"/> carries a vendor failure —
    /// the resume checker treats that as "try again next tick", never as the Run's fault.
    /// </summary>
    public async Task<ConversationResult> AnswersFor(
        Run run,
        CancellationToken cancellationToken = default
    )
    {
        if (run.WaitingSince is not { } since)
        {
            return new ConversationResult([], Failure: null);
        }

        var result = await conversation.ReadSince(
            run.ProjectId,
            run.VendorStoryId,
            since,
            cancellationToken
        );

        return result.Failure is not null
            ? result
            : result with
            {
                Comments =
                [
                    .. result.Comments.Where(comment => !RunMarker.IsAgentComment(comment.Body)),
                ],
            };
    }
}
