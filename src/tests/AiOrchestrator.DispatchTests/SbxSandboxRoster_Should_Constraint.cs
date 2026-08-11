using AiOrchestrator.ServiceDefaults.Agents.Sbx;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// What this host claims on the machine (#311) — the one predicate the startup sweep deletes by and the
/// sandboxes surface enters by.
/// <para>
/// Worth its own test because the two callers pull in opposite directions. Too narrow and the sweep
/// leaks sandboxes it made; too wide and the surface becomes a way into a sandbox this product never
/// created. The names below are the real ones this machine produces, not invented examples.
/// </para>
/// </summary>
public class SbxSandboxRoster_Should_Constraint
{
    [Theory]
    // The two prefixes the host creates and reaps.
    [InlineData("aio-run-3f2a9c1d4e5b6a7c8d9e0f1a", true)]
    [InlineData("aio-probe-3f2a9c1d4e5b6a7c8d9e0f1a", true)]
    // A real sandbox on the developer's machine, made by another tool. Entering it would make this
    // product a way into machines it does not own.
    [InlineData("opencode-ds-connect", false)]
    // `aio-` is shorthand for two prefixes and not a wildcard: these are host paths, not sandboxes.
    [InlineData("aio-carry-1234", false)]
    [InlineData("aio-workspace-1234", false)]
    [InlineData("aio-", false)]
    // Prefix matching is anchored — a name that merely contains ours is not ours.
    [InlineData("not-aio-run-1234", false)]
    [InlineData("", false)]
    public void ThePredicate_Should_ClaimExactlyTheHostsOwnTwoPrefixes(string name, bool claimed) =>
        SbxSandboxRoster.Claims(name).ShouldBe(claimed);

    [Fact]
    public void ThePredicate_Should_TreatAnAbsentNameAsNotOurs() =>
        SbxSandboxRoster.Claims(null).ShouldBeFalse();

    [Fact]
    public async Task TheRoster_Should_ReadTheJsonListingAndKeepOnlyWhatThisHostClaims()
    {
        // The shape `sbx ls --json` really returns, verified against the CLI on 2026-08-11.
        var cli = Cli(
            """
            {"sandboxes":[
              {"name":"aio-run-alive","id":"1","agent":"claude","status":"running","workspaces":["/w/one"]},
              {"name":"aio-probe-old","id":"2","agent":"claude","status":"stopped","workspaces":[]},
              {"name":"opencode-ds-connect","id":"3","agent":"opencode","status":"stopped","workspaces":["/w/two"]}
            ]}
            """
        );

        var claimed = await SbxSandboxRoster.Claimed(cli, CancellationToken.None);

        claimed.Select(entry => entry.Name).ShouldBe(["aio-run-alive", "aio-probe-old"]);
        claimed[0].Status.ShouldBe("running");
        claimed[0].Workspace.ShouldBe("/w/one");

        // Status is carried, not interpreted: entering a stopped sandbox starts it, so the surface
        // needs the real word rather than a boolean somebody guessed the meaning of.
        claimed[1].Status.ShouldBe("stopped");
        claimed[1].Workspace.ShouldBeNull();
    }

    [Fact]
    public async Task TheRoster_Should_AnswerNothingWhenTheCliCannotAnswer()
    {
        // "Cannot tell" must read as "nothing to act on": a broken daemon that reaped or listed
        // arbitrarily would be worse than one that does neither.
        var claimed = await SbxSandboxRoster.Claimed(Cli("", exitCode: 1), CancellationToken.None);

        claimed.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheRoster_Should_AnswerNothingWhenTheCliOutputShapeMoves()
    {
        // A CLI upgrade that changes its JSON must not throw out of a startup sweep.
        var claimed = await SbxSandboxRoster.Claimed(
            Cli("not json at all"),
            CancellationToken.None
        );

        claimed.ShouldBeEmpty();
    }

    static SbxCli Cli(string stdout, int exitCode = 0)
    {
        var directory = Directory.CreateTempSubdirectory("sbx-roster-").FullName;
        var script = Path.Combine(directory, "sbx.sh");

        File.WriteAllText(
            script,
            $"""
            #!/bin/sh
            cat <<'STDOUT'
            {stdout}
            STDOUT
            exit {exitCode}

            """
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        return new SbxCli(
            new SbxSandboxOptions
            {
                CommandPath = script,
                Memory = "1g",
                InjectedSecrets = [],
                SessionFiles = [],
            }
        );
    }
}
