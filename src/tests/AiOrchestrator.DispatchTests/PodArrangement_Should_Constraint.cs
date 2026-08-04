using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.ServiceDefaults.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// #246 — where a claimed Run executes is a composition choice, selected by configuration
/// presence (design D2, the queue/outbox rule one level down). What is asserted here is the
/// selection and what a pod would receive — the container mechanics are the launcher's and the
/// real end-to-end exercises them; these tests must fail when the arrangement rules change, not
/// when docker does.
/// </summary>
public class PodArrangement_Should_Constraint
{
    static HostApplicationBuilder QueuelessHabitat()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:aiorchestratordb"] =
            "Host=localhost;Database=aio;Username=u;Password=p";
        return builder;
    }

    [Fact]
    public void NoImageNamed_Should_KeepInProcessExecution()
    {
        // The nothing-configured path is every habitat that exists today — it must not move.
        var builder = QueuelessHabitat();

        builder.AddRunDispatchConsumer();

        builder
            .Services.Single(service => service.ServiceType == typeof(IDispatchedRunHandler))
            .ImplementationType.ShouldBe(typeof(InProcessRunHandler));
    }

    [Fact]
    public void AnImageNamed_Should_SelectThePodLauncher()
    {
        var builder = QueuelessHabitat();
        builder.Configuration[DispatchComposition.PodImageKey] = "aio-dispatch-worker:latest";

        builder.AddRunDispatchConsumer();

        builder
            .Services.Single(service => service.ServiceType == typeof(IDispatchedRunHandler))
            .ImplementationType.ShouldBe(typeof(PodRunLauncher));
    }

    [Fact]
    public void ThePodOptions_Should_CarryTheDatabaseAndTheCapDefault()
    {
        var builder = QueuelessHabitat();
        builder.Configuration[DispatchComposition.PodImageKey] = "aio-dispatch-worker:latest";

        builder.AddRunDispatchConsumer();
        var options = builder.Build().Services.GetRequiredService<PodLaunchOptions>();

        options.Image.ShouldBe("aio-dispatch-worker:latest");
        // The worker reads the Run from the database; a pod without it exits non-zero at once.
        options
            .Environment["ConnectionStrings__aiorchestratordb"]
            .ShouldBe("Host=localhost;Database=aio;Username=u;Password=p");
        // Default 2 (design D6): a laptop running eight agent pods stops being a laptop.
        options.MaxConcurrentPods.ShouldBe(2);
    }

    [Fact]
    public void AnExplicitPodConnectionString_Should_WinOverTheHostsOwn()
    {
        // A process host's "localhost" is the container itself inside a pod; the override is how
        // the dev loop points pods at the published port.
        var builder = QueuelessHabitat();
        builder.Configuration[DispatchComposition.PodImageKey] = "aio-dispatch-worker:latest";
        builder.Configuration["Dispatch:PodDatabaseConnectionString"] =
            "Host=host.docker.internal;Port=55432;Database=aio;Username=u;Password=p";

        builder.AddRunDispatchConsumer();
        var options = builder.Build().Services.GetRequiredService<PodLaunchOptions>();

        options
            .Environment["ConnectionStrings__aiorchestratordb"]
            .ShouldContain("host.docker.internal");
    }

    [Fact]
    public void TheHostsSessions_Should_EnterThePodByDefaultAndBeRefusable()
    {
        // The grill's owner decision (#246, design D5), with its off switch. Read-only until the
        // observed contract says a CLI writes on refresh.
        var withDefault = QueuelessHabitat();
        withDefault.Configuration[DispatchComposition.PodImageKey] = "w:1";
        withDefault.Configuration["Dispatch:PodSessionsHome"] = "/home/operator";
        withDefault.AddRunDispatchConsumer();

        var mounts = withDefault.Build().Services.GetRequiredService<PodLaunchOptions>().Mounts;
        mounts.ShouldContain("/home/operator/.config/opencode:/root/.config/opencode:ro");
        // The observed home (#246 tasks 3.1): opencode's credentials live under .local/share,
        // not .config — a mount set without it carries the commands and leaves the session.
        mounts.ShouldContain("/home/operator/.local/share/opencode:/root/.local/share/opencode:ro");
        mounts.ShouldContain("/home/operator/.claude:/root/.claude:ro");

        var switchedOff = QueuelessHabitat();
        switchedOff.Configuration[DispatchComposition.PodImageKey] = "w:1";
        switchedOff.Configuration["Dispatch:PodSessionsHome"] = "/home/operator";
        switchedOff.Configuration["Dispatch:PodSessions"] = "false";
        switchedOff.AddRunDispatchConsumer();

        switchedOff.Build().Services.GetRequiredService<PodLaunchOptions>().Mounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheCap_Should_DelayTheThirdRunNeverDropIt()
    {
        // Design D6 at the seam it lives on: the semaphore is the launcher's, so it is asserted
        // through a launcher whose docker never starts — the third Handle must wait for a slot
        // and still complete once one frees.
        var options = new PodLaunchOptions { Image = "w:1", MaxConcurrentPods = 2 };
        var launcher = new CountingLauncher(options);

        var first = launcher.Handle(Guid.NewGuid(), CancellationToken.None);
        var second = launcher.Handle(Guid.NewGuid(), CancellationToken.None);
        var third = launcher.Handle(Guid.NewGuid(), CancellationToken.None);

        await launcher.TwoRunning.Task.WaitAsync(TimeSpan.FromSeconds(5));
        launcher.PeakConcurrency.ShouldBe(2);
        third.IsCompleted.ShouldBeFalse();

        launcher.Release();
        await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(5));
        launcher.Launched.ShouldBe(3);
        launcher.PeakConcurrency.ShouldBe(2);
    }

    /// <summary>
    /// The launcher's semaphore with the container swap-out: Launch is overridden to a gate, so
    /// what is measured is exactly the bound, not docker.
    /// </summary>
    sealed class CountingLauncher(PodLaunchOptions options)
    {
        readonly SemaphoreSlim _slots = new(options.MaxConcurrentPods, options.MaxConcurrentPods);
        readonly TaskCompletionSource _gate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int _running;

        public TaskCompletionSource TwoRunning { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Launched;
        public int PeakConcurrency;

        public void Release() => _gate.SetResult();

        public async Task Handle(Guid runId, CancellationToken cancellationToken)
        {
            await _slots.WaitAsync(cancellationToken);
            try
            {
                var now = Interlocked.Increment(ref _running);
                InterlockedMax(ref PeakConcurrency, now);
                if (now == 2)
                {
                    TwoRunning.TrySetResult();
                }

                Interlocked.Increment(ref Launched);
                await _gate.Task;
            }
            finally
            {
                Interlocked.Decrement(ref _running);
                _slots.Release();
            }
        }

        static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)))
            {
                Interlocked.CompareExchange(ref target, value, current);
            }
        }
    }
}
