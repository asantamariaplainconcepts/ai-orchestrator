using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.ServiceDefaults.Dispatch;
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
    public void Composition_Should_RefuseToStartWithoutAQueue()
    {
        // Failing at startup beats failing on the first Run, when a human is no longer watching.
        var builder = Host.CreateApplicationBuilder();

        Should
            .Throw<InvalidOperationException>(() => builder.AddRunDispatch())
            .Message.ShouldContain("queues");
    }
}
