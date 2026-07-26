using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.ServiceDefaults.Dispatch;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The substrate's contract, against a real Azurite queue.
/// <para>
/// The test that matters most is <see cref="Claim_Should_NotRedeliverAfterAConsumerDies"/>:
/// everything else here would also pass on an at-least-once queue, which is precisely the
/// behaviour BR-004 forbids. A green "enqueue then claim" proves the plumbing and nothing about
/// the rule.
/// </para>
/// </summary>
[Collection(DispatchQueueCollection.Name)]
public class DispatchSubstrate_Should_Constraint(DispatchQueueFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetQueue();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dispatch_Should_DeliverTheRunIdItWasGiven()
    {
        var runId = Guid.CreateVersion7();

        await new QueueRunDispatcher(fixture.Queues).Dispatch(runId);

        (await new DispatchQueueReader(fixture.Queues).Claim()).ShouldBe(runId);
    }

    [Fact]
    public async Task Claim_Should_NotRedeliverAfterAConsumerDies()
    {
        // The heart of BR-004. A consumer claims the message and then "dies" — nothing further
        // happens, no completion, no acknowledgement. On a stock at-least-once queue the message
        // would reappear after its visibility timeout and KEDA would start a second job: an
        // automatic retry the product forbids. Because Claim deletes before returning, it cannot.
        await new QueueRunDispatcher(fixture.Queues).Dispatch(Guid.CreateVersion7());

        var reader = new DispatchQueueReader(fixture.Queues);
        (await reader.Claim()).ShouldNotBeNull();

        // Simulated crash: no further interaction with the queue at all.

        (await fixture.QueueDepth()).ShouldBe(0, "a claimed message must not survive its consumer");
        (await reader.Claim()).ShouldBeNull(
            "re-delivery would be the automatic retry BR-004 forbids"
        );
    }

    [Fact]
    public async Task Claim_Should_ReturnNullOnAnEmptyQueue()
    {
        (await new DispatchQueueReader(fixture.Queues).Claim()).ShouldBeNull();
    }

    [Fact]
    public async Task Claim_Should_DiscardAMessageItCannotUnderstand()
    {
        // Written by a future version, or by something that is not us at all. Nothing will ever
        // make it parse, and BR-004 leaves no retry that could — so it is consumed and dropped
        // rather than crashing the worker into a loop it cannot exit.
        var queue = fixture.Queues.GetQueueClient(DispatchQueue.Name);
        await queue.SendMessageAsync("""{"v":99,"runId":"not-a-guid"}""");

        (await new DispatchQueueReader(fixture.Queues).Claim()).ShouldBeNull();
        (await fixture.QueueDepth()).ShouldBe(
            0,
            "an unreadable message is removed, not left to block the queue"
        );
    }

    [Fact]
    public async Task Dispatch_Should_PreserveOrderAcrossSeveralRuns()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var dispatcher = new QueueRunDispatcher(fixture.Queues);

        await dispatcher.Dispatch(first);
        await dispatcher.Dispatch(second);

        var reader = new DispatchQueueReader(fixture.Queues);
        (await reader.Claim()).ShouldBe(first);
        (await reader.Claim()).ShouldBe(second);
    }

    [Fact]
    public void Message_Should_RejectAShapeItDoesNotUnderstand()
    {
        DispatchMessage.TryParse("not json at all").ShouldBeNull();
        DispatchMessage
            .TryParse("""{"v":2,"runId":"0195f0a0-0000-7000-8000-000000000000"}""")
            .ShouldBeNull();
        DispatchMessage
            .TryParse("""{"v":1,"runId":"00000000-0000-0000-0000-000000000000"}""")
            .ShouldBeNull();
    }
}
