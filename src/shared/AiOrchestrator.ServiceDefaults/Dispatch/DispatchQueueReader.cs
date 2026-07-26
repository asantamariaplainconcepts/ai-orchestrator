using AiOrchestrator.BuildingBlocks.Dispatch;
using Azure.Storage.Queues;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// The consuming half of the substrate — and the one place BR-004 is enforced.
/// <para>
/// Storage Queues are at-least-once: a message whose consumer dies before deleting it becomes
/// visible again, and KEDA starts another job. That is an automatic retry, which BR-004 forbids
/// outright ("a <c>Failed</c> Run is terminal; humans re-trigger"). The two cannot both hold, so
/// the rule wins: <see cref="Claim"/> <b>deletes the message before returning it</b>, and
/// therefore before any work happens.
/// </para>
/// <para>
/// The cost is real and deliberate. A job killed by infrastructure — an evicted node, an image
/// that will not start — is now indistinguishable from an Agent that failed: both end the Run
/// <c>Failed</c>, and both need a human. That is the trade BR-004 already makes everywhere else,
/// and the remedy already exists in the product as <i>Run now</i> (BR-013).
/// </para>
/// </summary>
public sealed class DispatchQueueReader(QueueServiceClient service)
{
    /// <summary>
    /// Takes at most one message, deletes it, and returns what it carried — or null when the
    /// queue is empty or the message was unreadable.
    /// </summary>
    public async Task<Guid?> Claim(CancellationToken cancellationToken = default)
    {
        var queue = service.GetQueueClient(DispatchQueue.Name);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var received = await queue.ReceiveMessageAsync(cancellationToken: cancellationToken);
        var message = received?.Value;

        if (message is null)
        {
            return null;
        }

        // Delete first. Everything after this point may fail without the message coming back —
        // which is the entire point, not an oversight.
        await queue.DeleteMessageAsync(
            message.MessageId,
            message.PopReceipt,
            cancellationToken: cancellationToken
        );

        // An unparseable or unknown-version message is dropped, not retried and not crashed on:
        // nothing will ever make it parse, and BR-004 leaves no mechanism that would.
        return DispatchMessage.TryParse(message.MessageText)?.RunId;
    }
}
