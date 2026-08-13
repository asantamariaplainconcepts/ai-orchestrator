using System.Text;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Runs.Features.Observation;
using Microsoft.AspNetCore.SignalR;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The half #304 and #311 never pinned: a terminal that OPENS, and whose bytes reach the caller. Every
/// other terminal test asserts a refusal, so the pump — the thing a working terminal actually is — had no
/// coverage, and a shell that opens and then says nothing looked exactly like success (#326's sentence,
/// and it was right).
///
/// <para>
/// <b>Restored in a form CI can run</b> (#329). The version #327 removed drove SignalR through
/// <c>TestServer</c> and waited ten seconds for one long-polling message: that holds on a developer
/// machine and never held on a two-core runner behind a full suite. It failed on its own pull request, on
/// the merge, and on every pull request after.
/// </para>
/// <para>
/// <b>The transport is not made faster here — it is removed.</b> #330 made the pump a synchronous loop,
/// so this test calls it directly with a fake client and a fake terminal. It returns when the terminal
/// ends, so there is <b>no wait to budget at all</b>: nothing polls, nothing sleeps, and there is no
/// wall-clock number anywhere in this file to be wrong on a slower machine (#329 criteria 2 and 3).
/// </para>
/// <para>
/// What is given up, stated rather than hidden: this no longer exercises SignalR's own wiring — the hub
/// method, the guard order, the connection lifetime. Those are covered by the refusal tests beside it,
/// which do drive the transport and pass reliably because they assert something that happens
/// immediately. What was never covered, and is covered here, is the loop in between.
/// </para>
/// </summary>
public class RunTerminalOutput_Should_Constraint
{
    [Fact]
    public void AShellThatSpeaks_Should_ReachTheCallerThatOpenedIt()
    {
        var client = new RecordingClient();
        using var terminal = new SpeakingTerminal("hello$ ");

        RunTerminalHub.Pump(client, terminal, connection: "c1", runId: Guid.NewGuid());

        var output = client
            .Sent.Where(sent => sent.Method == "output")
            .Select(sent => Encoding.UTF8.GetString((byte[])sent.Argument!))
            .ToList();

        output.ShouldHaveSingleItem().ShouldBe("hello$ ");
    }

    /// <summary>
    /// The other half of "a working terminal": the client is told when the shell ends, so the surface can
    /// say the sandbox is gone instead of leaving a dead terminal on screen (#311 criterion 6).
    /// </summary>
    [Fact]
    public void AShellThatEnds_Should_TellTheCallerItEnded()
    {
        var client = new RecordingClient();
        using var terminal = new SpeakingTerminal("bye");

        RunTerminalHub.PumpSandbox(client, terminal, connection: "c2", sandbox: "aio-run-fake");

        client.Sent.Select(sent => sent.Method).ShouldBe(["output", "ended"]);
    }

    /// <summary>
    /// #329 criterion 4. The fake this replaces called <c>Thread.Sleep(Timeout.Infinite)</c> after
    /// speaking, to keep the terminal "open" — a thread nothing could ever release, parked once per test
    /// run. This fake ends its stream instead, so the pump returns on its own and there is nothing to
    /// release. Asserted rather than assumed: the disposal flag proves the pump reached the end.
    /// </summary>
    [Fact]
    public void TheFakeTerminal_Should_LeaveNoThreadParked()
    {
        var client = new RecordingClient();
        var terminal = new SpeakingTerminal("x");

        RunTerminalHub.Pump(client, terminal, connection: "c3", runId: Guid.NewGuid());
        terminal.Dispose();

        terminal.Reads.ShouldBe(2, "one read that spoke, one that reported end of stream");
        terminal.Disposed.ShouldBeTrue();
    }

    /// <summary>Says one thing, then reports end of stream. Never blocks, so nothing can park on it.</summary>
    sealed class SpeakingTerminal(string line) : IRunTerminal
    {
        public int Reads { get; private set; }

        public bool Disposed { get; private set; }

        public int Read(byte[] buffer)
        {
            Reads++;

            // 0 is how a terminal ends — the shell exited, or the sandbox went with its Run. Returning it
            // on the second read is what lets the pump finish and this test be synchronous.
            if (Reads > 1)
            {
                return 0;
            }

            var bytes = Encoding.UTF8.GetBytes(line);
            bytes.CopyTo(buffer, 0);
            return bytes.Length;
        }

        public void Write(ReadOnlySpan<byte> data) { }

        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// Records what the hub sent. Completes synchronously, which is the point: the pump blocks on each
    /// send (#330), so a fake that completed asynchronously would reintroduce the scheduling this test is
    /// deliberately not exercising.
    /// </summary>
    sealed class RecordingClient : ISingleClientProxy
    {
        public List<(string Method, object? Argument)> Sent { get; } = [];

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            Sent.Add((method, args.Length > 0 ? args[0] : null));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException("the terminal only sends");
    }
}
