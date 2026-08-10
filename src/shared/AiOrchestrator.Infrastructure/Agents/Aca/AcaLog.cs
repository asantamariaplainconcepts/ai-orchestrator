using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

static partial class AcaLog
{
    [LoggerMessage(
        EventId = 4130,
        Level = LogLevel.Information,
        Message = "Not yet authorised on sandbox group {Group}; waiting for the role to propagate"
    )]
    public static partial void WaitingForRole(ILogger logger, string group);

    [LoggerMessage(
        EventId = 6260,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} created"
    )]
    public static partial void Created(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 6261,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} disposed"
    )]
    public static partial void Disposed(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 6262,
        Level = LogLevel.Error,
        Message = "Sandbox {Sandbox} could not be disposed and may still be running: {Detail}"
    )]
    public static partial void NotDisposed(ILogger logger, string sandbox, string detail);

    [LoggerMessage(
        EventId = 6263,
        Level = LogLevel.Warning,
        Message = "Sandbox {Sandbox} could not publish its preview port: {Detail}"
    )]
    public static partial void PreviewNotPublished(ILogger logger, string sandbox, string detail);
}
