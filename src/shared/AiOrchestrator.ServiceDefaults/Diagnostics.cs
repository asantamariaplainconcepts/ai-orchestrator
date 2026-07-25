using System.Diagnostics;

namespace AiOrchestrator.ServiceDefaults;

/// <summary>The one <see cref="ActivitySource"/> for application-authored spans.</summary>
public static class Diagnostics
{
    public const string ActivitySourceName = "AiOrchestrator";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
