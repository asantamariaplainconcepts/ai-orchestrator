using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// Composition rules that fail at runtime rather than at compile time, so they need a test.
/// Dispatch has one substrate since #296 — the Postgres outbox — and the interesting rules are
/// that it composes through the real integration-events pipeline, and that the retired queue is
/// refused by name rather than silently ignored.
/// </summary>
public class DispatchComposition_Should_Constraint
{
    [Fact]
    public void TheOutbox_Should_BeTheOneSubstrate()
    {
        // The real composition order, not a convenient subset: the outbox substrate publishes
        // through the CAP that integration events compose, and a test that skipped that would
        // prove the registration exists rather than that the habitat works.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:aiorchestratordb"] =
            "Host=localhost;Database=aio;Username=u;Password=p";
        builder.AddIntegrationEvents();

        builder.AddRunDispatch();

        builder
            .Build()
            .Services.GetRequiredService<IRunDispatcher>()
            .ShouldBeOfType<OutboxRunDispatcher>();
    }

    [Fact]
    public void NoDatabase_Should_RefuseToStart()
    {
        // Failing at startup beats failing on the first Run, when a human is no longer watching.
        var builder = Host.CreateApplicationBuilder();

        var message = Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatch())
            .Message;

        message.ShouldContain("aiorchestratordb");
        message.ShouldContain("outbox");
    }

    [Fact]
    public void TheRetiredQueue_Should_BeRefusedNamingWhatReplacedIt()
    {
        // DEC-013's substrate. A habitat still naming it must meet the sentence, not silence:
        // a key that quietly stopped meaning anything is how a deployment ends up running
        // something nobody chose — the same treatment the retired pod image gets.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:aiorchestratordb"] =
            "Host=localhost;Database=aio;Username=u;Password=p";
        builder.Configuration["ConnectionStrings:queues"] =
            "https://staiodev1234.queue.core.windows.net/";

        var message = Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatch())
            .Message;

        message.ShouldContain(DispatchComposition.RetiredQueueConnectionName);
        message.ShouldContain("no longer exists");
        message.ShouldContain("outbox");
    }

    [Fact]
    public void TheConsumer_Should_RefuseTheRetiredQueueToo()
    {
        // Both entry points meet the same sentence, so the refusal cannot depend on which half
        // of dispatch a habitat composes first.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:aiorchestratordb"] =
            "Host=localhost;Database=aio;Username=u;Password=p";
        builder.Configuration["ConnectionStrings:queues"] = "https://example/queue";

        Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatchConsumer())
            .Message.ShouldContain(DispatchComposition.RetiredQueueConnectionName);
    }

    [Fact]
    public void TheHabitat_Should_ComposeTheConsumer()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:aiorchestratordb"] =
            "Host=localhost;Database=aio;Username=u;Password=p";

        builder.AddRunDispatchConsumer();

        builder
            .Services.Any(service => service.ServiceType == typeof(OutboxRunSubscriber))
            .ShouldBeTrue();
    }
}
