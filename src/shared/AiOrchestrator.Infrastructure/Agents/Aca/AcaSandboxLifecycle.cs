using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>Creates, groups, disposes of, and reports readiness for one Azure sandbox (#296 design D4).</summary>
sealed class AcaSandboxLifecycle(AcaSandboxOptions options, AcaCli cli, ILogger logger)
{
    /// <summary>
    /// The Project's own SandboxGroup (design D4). The configured name is a template: where it
    /// contains <c>{project}</c> the Project's id fills it, so one setting describes a deployment
    /// whose groups are per Project rather than requiring one key per Project.
    /// <para>
    /// A Run with no Project — there is no such thing today, and the readiness probe's own
    /// sandboxes are not Runs — falls back to the template as written, which is what a habitat
    /// that never templated it meant anyway.
    /// </para>
    /// </summary>
    public string GroupFor(Guid? projectId) =>
        projectId is { } id
            ? options.SandboxGroup.Replace(
                "{project}",
                id.ToString("N"),
                StringComparison.OrdinalIgnoreCase
            )
            : options.SandboxGroup.Replace(
                "{project}",
                "shared",
                StringComparison.OrdinalIgnoreCase
            );

    public async Task<string> Create(string group, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "sandbox",
            "create",
            "--group",
            group,
            .. (
                options.DiskId is { Length: > 0 } diskId
                    ? new[] { "--disk-id", diskId }
                    : ["--disk", options.Disk]
            ),
            .. options.Credentials.SelectMany(id => new[] { "--credential", id }),
            "-o",
            "json",
        ];

        var created = await cli.Run(arguments, cancellationToken);

        // **Role propagation, waited out rather than reported as a fault (task 4.4).** The spike
        // watched a freshly granted `Container Apps SandboxGroup Data Owner` answer 403 for about
        // a minute before it began working. A deployment provisioned minutes ago would fail its
        // first Runs for a reason that fixes itself, and BR-004 means nothing retries them — so
        // the failure would be permanent for a condition that was temporary.
        //
        // Bounded, and only for authorization. A 403 that is really a missing grant still fails,
        // one minute later, saying so. Everything else fails at once: retrying a bad disk name
        // would only delay the sentence an operator needs.
        for (
            var attempt = 0;
            attempt < options.AuthorizationAttempts
                && created.ExitCode != 0
                && AcaCli.IsAuthorization(created);
            attempt++
        )
        {
            AcaLog.WaitingForRole(logger, group);
            await Task.Delay(options.AuthorizationRetryDelay, cancellationToken);
            created = await cli.Run(arguments, cancellationToken);
        }

        if (created.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                AcaCli.IsAuthorization(created)
                    ? "This deployment is not authorised to create sandboxes in its group, and it "
                        + $"still was not after {options.AuthorizationAttempts} attempts over "
                        + $"{options.AuthorizationAttempts * options.AuthorizationRetryDelay.TotalSeconds:0}s. "
                        + "Grant 'Container Apps SandboxGroup Data Owner' on the group to the "
                        + $"identity this process runs as. ({AcaCli.Detail(created)})"
                    : $"The sandbox for this Run could not be created. ({AcaCli.Detail(created)})"
            );
        }

        var id = SandboxId(created.Stdout);
        if (id is null)
        {
            throw new AgentProcessHostException(
                "The sandbox was created but its id could not be read from the response, so it "
                    + "can neither be used nor cleaned up. Refusing rather than leaking it."
            );
        }

        AcaLog.Created(logger, id);
        return id;
    }

    /// <summary>
    /// Disposal survives cancellation, like every other launcher here: an abandoned sandbox is the
    /// leak, and it costs money as well as attention.
    /// </summary>
    public async Task Dispose(string sandbox)
    {
        try
        {
            await cli.Run(["sandbox", "delete", "--id", sandbox, "--yes"], CancellationToken.None);
            AcaLog.Disposed(logger, sandbox);
        }
        catch (Exception exception)
        {
            AcaLog.NotDisposed(logger, sandbox, exception.Message);
        }
    }

    // ---- Readiness (design D6 of #279) ----

    public async Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken)
    {
        try
        {
            var doctor = await cli.Run(["sandbox", "list", "-o", "json"], cancellationToken);

            return doctor.ExitCode == 0
                ? new AgentHostReadiness(
                    Ready: true,
                    Where: $"a per-Run sandbox in {options.SandboxGroup}",
                    Remedy: null
                )
                : new AgentHostReadiness(
                    Ready: false,
                    Where: $"a per-Run sandbox in {options.SandboxGroup}",
                    Remedy: "The sandbox group could not be reached. Check that this deployment's "
                        + "identity still holds the Container Apps SandboxGroup Data Owner role on "
                        + $"'{options.SandboxGroup}' — a newly granted one takes about a minute to "
                        + $"propagate. ({AcaCli.Detail(doctor)})"
                );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new AgentHostReadiness(
                Ready: false,
                Where: $"a per-Run sandbox in {options.SandboxGroup}",
                Remedy: $"The sandbox platform could not be asked whether it is ready: {exception.Message}"
            );
        }
    }

    /// <summary>The sandbox id out of a `create -o json` response, without taking a JSON dependency
    /// on a preview surface whose shape is expected to move.</summary>
    internal static string? SandboxId(string stdout)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            stdout,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );

        return match.Success ? match.Value : null;
    }
}
