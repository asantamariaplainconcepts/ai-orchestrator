using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace AiOrchestrator.ServiceDefaults;

/// <summary>
/// Feature state, composed from <c>IConfiguration</c> and from nothing else (#331, design D6).
/// <para>
/// <b>This seam has no consumer, and that is the honest description of it.</b> RULE-007 names a
/// speculative abstraction an anti-pattern, and this is one: nothing in this change asks the feature
/// manager anything. It is here because the owner decided (#331) that the follow-on capability —
/// choosing a Run's isolation substrate, sbx or worktree, per Automation — should arrive against
/// composition that already exists rather than pay for it then. Written down rather than
/// rationalised, because the next reader will otherwise correctly identify it as an abstraction
/// nobody asked for and delete it without knowing what it was for.
/// </para>
/// <para>
/// <b>No Azure App Configuration.</b> The library is built on <c>IConfiguration</c>; the Azure
/// service is one possible source, not a requirement. Keeping it out is what keeps DEC-049's
/// promise true — a stranger with Docker can still run this, because nothing in the start path
/// reaches for a cloud endpoint or a credential to read a feature flag.
/// </para>
/// <para>
/// A habitat that declares no <c>FeatureManagement</c> section starts exactly as it did before this
/// existed: the manager resolves, answers "off" for everything nobody declared, and no behaviour any
/// existing scenario can observe differs.
/// </para>
/// </summary>
public static class FeatureManagementComposition
{
    public static TBuilder AddFeatureState<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // The parameterless overload binds the host's own IConfiguration — the "FeatureManagement"
        // section when one exists, and nothing at all when it does not.
        builder.Services.AddFeatureManagement();
        return builder;
    }
}
