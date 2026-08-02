using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// Composition rules that fail at runtime rather than at compile time, so they need a test.
/// <para>
/// The endpoint setting has two legal shapes — a URI when a managed identity supplies the
/// credential, a keyed connection string when Azurite does — and passing the wrong one to the
/// wrong constructor throws only when the first message is dispatched. That is far too late to
/// find out, so the discrimination is asserted here.
/// </para>
/// </summary>
public class DispatchComposition_Should_Constraint
{
    const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;";

    [Fact]
    public void Composition_Should_AcceptAKeyedConnectionString()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:queues"] = AzuriteConnectionString;

        builder.AddRunDispatch();

        builder
            .Build()
            .Services.GetRequiredService<IRunDispatcher>()
            .ShouldBeOfType<QueueRunDispatcher>();
    }

    [Fact]
    public void Composition_Should_AcceptAnEndpointUri()
    {
        // The deployed shape: no key anywhere, the identity supplies the credential (BR-010).
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:queues"] =
            "https://staiodev1234.queue.core.windows.net/";

        builder.AddRunDispatch();

        builder
            .Build()
            .Services.GetRequiredService<IRunDispatcher>()
            .ShouldBeOfType<QueueRunDispatcher>();
    }

    [Fact]
    public void NoQueue_Should_ComposeTheOutboxSubstrate()
    {
        // #225: the absence of a queue is a habitat, not a misconfiguration. It says "dispatch
        // through the outbox this database already holds", which is how self-hosting drops a
        // container.
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
    public void NeitherSubstrate_Should_RefuseToStart()
    {
        // Failing at startup beats failing on the first Run, when a human is no longer watching —
        // and naming *both* contracts is what stops the reader guessing which one was meant.
        var builder = Host.CreateApplicationBuilder();

        var message = Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatch())
            .Message;

        message.ShouldContain("queues");
        message.ShouldContain("aiorchestratordb");
    }

    [Fact]
    public void AQueueHabitat_Should_RefuseAnInProcessConsumer()
    {
        // The dangerous composition, refused where it would be made (design D2): a host holding
        // both sides puts the portal's identity and the worker's on the same process, which is
        // the boundary `infra/dev/dispatch.tf` exists to keep.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:queues"] = AzuriteConnectionString;

        Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatchConsumer())
            .Message.ShouldContain("compromise cannot reach both");
    }

    [Fact]
    public void AQueuelessHabitat_Should_ComposeTheConsumer()
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
