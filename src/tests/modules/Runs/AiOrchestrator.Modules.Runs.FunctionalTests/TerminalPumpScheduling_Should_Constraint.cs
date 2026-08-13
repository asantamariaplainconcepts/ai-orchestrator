using AiOrchestrator.Modules.Runs.Features.Observation;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #330 — a terminal pump runs on a thread dedicated to it, never on a thread-pool worker.
/// <para>
/// The pool is sized for work that finishes and a pump never does: its first act is a blocking read that
/// does not return until the shell speaks, and the loop blocks again for as long as the terminal lives.
/// Every open terminal therefore used to occupy a pool worker permanently, competing with every request
/// the process serves.
/// </para>
/// <para>
/// <b>Asserted as a property of the thread, deliberately — never as a timing measurement.</b> The issue
/// asked for the absence of contention to be "demonstrated rather than asserted", which invites a
/// benchmark: open N terminals, race unrelated work, measure latency. That is the exact shape that
/// already failed here — #327 removed a terminal test because ten seconds of wall clock held on a
/// developer machine and never held on a two-core runner behind a full suite, and #329 exists to put
/// that coverage back in a form CI can run. A pump that is not on a pool thread cannot contend for a
/// pool worker, so the deterministic property below carries the same claim with none of the flakiness.
/// </para>
/// </summary>
public class TerminalPumpScheduling_Should_Constraint
{
    [Fact]
    public async Task Pump_Should_NotRunOnAThreadPoolWorker()
    {
        var observed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        RunTerminalHub.StartPump(() => observed.SetResult(Thread.CurrentThread.IsThreadPoolThread));

        var ranOnPoolThread = await observed.Task.WaitAsync(TimeSpan.FromSeconds(30));

        ranOnPoolThread.ShouldBeFalse(
            "a terminal pump blocks for the whole life of the terminal, so running it on a "
                + "thread-pool worker removes that worker from the pool for as long as the terminal is "
                + "open"
        );
    }

    /// <summary>
    /// The property <c>LongRunning</c> could not deliver, and the reason this issue needed more than the
    /// one-line change it looked like. A pump is <c>Read</c> → send → <c>Read</c>, so where it runs
    /// <i>after</i> the first send matters as much as where it starts: with
    /// <c>Task.Factory.StartNew(Func&lt;Task&gt;, …, LongRunning)</c> the dedicated thread exited at the
    /// first suspending await and every later read resumed on the pool. A synchronous loop on a real
    /// thread stays put, and this asserts that it does.
    /// </summary>
    [Fact]
    public async Task Pump_Should_StayOffThePoolAcrossItsWholeLoop()
    {
        var observations = new List<bool>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RunTerminalHub.StartPump(() =>
        {
            // Stands in for the loop: read, send, read. The send is what used to hand the rest of the
            // loop back to the pool, so the observation after it is the one that matters.
            observations.Add(Thread.CurrentThread.IsThreadPoolThread);
            Thread.Sleep(10);
            observations.Add(Thread.CurrentThread.IsThreadPoolThread);
            Thread.Sleep(10);
            observations.Add(Thread.CurrentThread.IsThreadPoolThread);
            done.SetResult();
        });

        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));

        observations.Count.ShouldBe(3);
        observations.ShouldAllBe(onPool => onPool == false);
    }

    /// <summary>
    /// The thread goes away when the pump returns. A dedicated thread that outlived its pump would trade
    /// a leaked pool worker for a leaked thread, which is not an improvement — and the fake terminal
    /// #329 describes parked one on <c>Thread.Sleep(Timeout.Infinite)</c> that nothing could release, so
    /// this is a live hazard here rather than a theoretical one.
    /// </summary>
    [Fact]
    public async Task Pump_Should_ReleaseItsThreadWhenItReturns()
    {
        var captured = new TaskCompletionSource<Thread>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RunTerminalHub.StartPump(() =>
        {
            captured.SetResult(Thread.CurrentThread);
            release.Task.GetAwaiter().GetResult();
        });

        var thread = await captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
        thread.IsAlive.ShouldBeTrue("the pump is still running");

        release.SetResult();

        // Derived rather than a guessed budget (#329 criterion 3): this polls for the thread to die and
        // returns the moment it does, so the deadline bounds only failure.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (thread.IsAlive && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        thread.IsAlive.ShouldBeFalse("the pump's thread is released when the pump returns");
    }
}
