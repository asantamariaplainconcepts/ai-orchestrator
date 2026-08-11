using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

static partial class SandboxLog
{
    [LoggerMessage(
        EventId = 4119,
        Level = LogLevel.Information,
        Message = "Opened a terminal in sandbox {Sandbox} for a human"
    )]
    public static partial void TerminalOpened(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 4118,
        Level = LogLevel.Warning,
        Message = "Removed {Count} sandbox(es) a previous process abandoned"
    )]
    public static partial void Reaped(ILogger logger, int count);

    [LoggerMessage(
        EventId = 4110,
        Level = LogLevel.Information,
        Message = "Created sandbox {Sandbox} for an agent"
    )]
    public static partial void Created(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 4111,
        Level = LogLevel.Information,
        Message = "Removed sandbox {Sandbox}"
    )]
    public static partial void Disposed(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 4115,
        Level = LogLevel.Information,
        Message = "Carried {File} into sandbox {Sandbox} — this Run acts as the machine owner's session"
    )]
    public static partial void SessionCarried(ILogger logger, string sandbox, string file);

    [LoggerMessage(
        EventId = 4116,
        Level = LogLevel.Warning,
        Message = "Could not carry {File} into sandbox {Sandbox}, so its runtime may not be signed in: {Detail}"
    )]
    public static partial void SessionNotCarried(
        ILogger logger,
        string sandbox,
        string file,
        string detail
    );

    [LoggerMessage(
        EventId = 4113,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} is serving a preview on host port {Port}"
    )]
    public static partial void PreviewPublished(ILogger logger, string sandbox, int port);

    [LoggerMessage(
        EventId = 4114,
        Level = LogLevel.Warning,
        Message = "Sandbox {Sandbox} published a preview port that could not be resolved, so this Run has no preview: {Detail}"
    )]
    public static partial void PreviewUnavailable(ILogger logger, string sandbox, string detail);

    [LoggerMessage(
        EventId = 4112,
        Level = LogLevel.Error,
        Message = "Sandbox {Sandbox} outlived its Run and could not be removed: {Detail}"
    )]
    public static partial void DisposalFailed(ILogger logger, string sandbox, string detail);
}
