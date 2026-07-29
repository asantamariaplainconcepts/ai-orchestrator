namespace AiOrchestrator.Modules.Backlog.Domain;

/// <summary>
/// The name the product gives a credential it stores itself (#124, design D2).
/// <para>
/// Derived rather than chosen, for three reasons. It cannot collide, because project ids do not.
/// It is idempotent, so rotating writes the same name and leaves no orphan behind. And it is
/// reconstructible, so an operator looking at a vault can tell which project a secret belongs to
/// without a lookup table.
/// </para>
/// <para>
/// The shape is constrained by the strictest store this product speaks to: Key Vault accepts
/// alphanumerics and hyphens only, up to 127 characters. This produces 49.
/// </para>
/// </summary>
static class ConnectorSecret
{
    public const string Prefix = "connector";

    public static string NameFor(Guid projectId, BacklogVendor vendor) =>
        $"{Prefix}-{vendor.ToString().ToLowerInvariant()}-{projectId:N}";
}
