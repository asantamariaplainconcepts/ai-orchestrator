using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>Corrects two of the platform's defaults that are actively wrong for a Run (design D3),
/// and reports what its policy denied.</summary>
sealed class AcaEgressPolicy(AcaSandboxOptions options, AcaCli cli)
{
    /// <summary>
    /// Auto-suspend is on at 600 s by default, and "idle" means no calls from <b>outside</b>:
    /// measured 2026-08-08, a sandbox went <c>Stopped</c> at t+41 s with a 60 s timeout <b>while a
    /// process wrote inside it every second</b>. An agent that thinks for ten minutes would be
    /// suspended mid-thought, so this is switched off for every sandbox a Run uses.
    /// </summary>
    public async Task DisableAutoSuspend(string sandbox, CancellationToken cancellationToken)
    {
        var set = await cli.Run(
            ["sandbox", "lifecycle", "set", "--id", sandbox, "--auto-suspend", "disable"],
            cancellationToken
        );

        if (set.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "Auto-suspend could not be disabled for this Run's sandbox, and the platform's "
                    + "default suspends a sandbox whose agent is thinking. Refusing rather than "
                    + $"running a Run that may stop halfway. ({AcaCli.Detail(set)})"
            );
        }
    }

    /// <summary>
    /// Deny-default egress is <b>opt-in, not the default</b>: measured 2026-08-08, a sandbox with
    /// no policy reached <c>example.com</c> and <c>pypi.org</c> with 200s while <c>egress show</c>
    /// reported none configured — whatever the platform's documentation says about denying by
    /// default. So the habitat's policy is applied before the agent starts, or the Run refuses.
    /// </summary>
    public async Task ApplyEgress(string sandbox, CancellationToken cancellationToken)
    {
        string[] rules =
        [
            .. options.EgressAllow.SelectMany(host => new[] { "--rule", $"{host}:Allow" }),
        ];

        var set = await cli.Run(
            ["sandbox", "egress", "set", "--id", sandbox, "--default", "Deny", .. rules],
            cancellationToken
        );

        if (set.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The egress policy could not be applied to this Run's sandbox. A sandbox with no "
                    + "policy has unrestricted outbound access, so the Run refuses rather than "
                    + $"executing an agent that can reach anything. ({AcaCli.Detail(set)})"
            );
        }
    }

    /// <summary>
    /// What the agent reached for and was refused (task 2.3). The platform keeps an auditable
    /// decision log per sandbox — timestamp, host, method and path — and a deny-default policy is
    /// only half a security story if nobody can see what it denied: an operator tightening an
    /// allow list needs the list of things that hit the wall, and a Member whose Run failed
    /// mysteriously deserves to see that its agent tried to reach somewhere it may not.
    /// <para>
    /// Forwarded through <c>onOutput</c> rather than logged host-side, so it lands where a Member
    /// already looks — in the Run's own output, beside the agent's.
    /// </para>
    /// <para>
    /// **This never fails a Run.** The work is finished by the time this is asked; a decision log
    /// that could not be read is worth a line in the output, never a Run marked failed for it.
    /// The cancellation token is deliberately not passed: a cancelled Run is precisely one whose
    /// denials are interesting.
    /// </para>
    /// </summary>
    public async Task ReportDeniedEgress(string sandbox, Action<string>? onOutput)
    {
        if (onOutput is null)
        {
            return;
        }

        AgentProcessOutcome decisions;
        try
        {
            decisions = await cli.Run(
                ["sandbox", "egress", "decisions", "--id", sandbox, "-o", "json"],
                CancellationToken.None
            );
        }
        catch (AgentProcessHostException)
        {
            return;
        }

        if (decisions.ExitCode != 0)
        {
            onOutput(
                "[egress] the sandbox's decision log could not be read, so what this Run reached "
                    + $"for is not recorded. ({AcaCli.Detail(decisions)})"
            );
            return;
        }

        string[] denied;
        try
        {
            denied = Denials(decisions.Stdout);
        }
        catch (JsonException)
        {
            // A preview surface whose shape is expected to move. When it does, the honest answer
            // is the log itself rather than silence — silence would read as "nothing was denied".
            onOutput(
                "[egress] the decision log arrived in a shape this build does not recognise; "
                    + "it is reproduced verbatim rather than dropped:"
            );
            onOutput($"[egress] {decisions.Stdout.Trim()}");
            return;
        }

        if (denied.Length == 0)
        {
            return;
        }

        onOutput(
            $"[egress] {denied.Length} outbound request(s) were denied by this habitat's policy:"
        );

        foreach (var line in denied)
        {
            onOutput($"[egress] {line}");
        }
    }

    /// <summary>
    /// The denied half of `aca sandbox egress decisions -o json`, as sentences.
    /// <para>
    /// Shaped from the real answer, measured 2026-08-09:
    /// <c>{"networkEgress":{"allowed":[],"denied":[{"timestamp":…,"host":…,"method":…,"path":…}]}}</c>.
    /// The first implementation filtered lines containing "Deny" — which the real output never
    /// contains, so a Run that reached for a blocked host said nothing at all. The stand-in had
    /// invented a table, and a fixture that invents its subject's answers can only ever confirm
    /// the invention (ADR-0016).
    /// </para>
    /// </summary>
    internal static string[] Denials(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (
            !document.RootElement.TryGetProperty("networkEgress", out var egress)
            || !egress.TryGetProperty("denied", out var denied)
            || denied.ValueKind != JsonValueKind.Array
        )
        {
            return [];
        }

        return
        [
            .. denied
                .EnumerateArray()
                .Select(entry =>
                {
                    var host = Text(entry, "host");
                    var method = Text(entry, "method");
                    var path = Text(entry, "path");
                    var at = Text(entry, "timestamp");

                    return string.Join(
                        ' ',
                        new[] { at, method, host + path }.Where(part => part.Length > 0)
                    );
                }),
        ];

        static string Text(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}
