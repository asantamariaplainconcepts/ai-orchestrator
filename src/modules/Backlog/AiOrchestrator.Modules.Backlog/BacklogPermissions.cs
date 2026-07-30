namespace AiOrchestrator.Modules.Backlog;

/// <summary>
/// What can be done to a Project's backlog (BR-009). Named per capability rather than per role, so a
/// new bundle is a change to the grant table and not to every declaration that mentions it.
/// <para>
/// Strings, and dotted, because that is what a declaration and a grant have to agree on across a
/// module boundary without either referencing the other. A test asserts every declared permission is
/// one of these constants, which is what makes a typo a red build instead of an operation only the
/// Admin bundle can reach.
/// </para>
/// </summary>
static class BacklogPermissions
{
    /// <summary>Read the mirrored backlog and its Stories. Observing (ACT-002).</summary>
    public const string Read = "backlog.read";

    /// <summary>Pull the vendor again now. A read of somebody else's system, not a configuration.</summary>
    public const string Refresh = "backlog.refresh";

    /// <summary>Apply or remove a trigger label on a Story (UC-007) — explicitly a Member act.</summary>
    public const string WriteLabel = "backlog.story.label.write";

    /// <summary>Point the Project at a repository, and hold its credential (UC-004).</summary>
    public const string Configure = "backlog.connector.configure";

    /// <summary>Ask the vendor whether the stored credential still works — inspecting configuration.</summary>
    public const string TestConnector = "backlog.connector.test";
}
