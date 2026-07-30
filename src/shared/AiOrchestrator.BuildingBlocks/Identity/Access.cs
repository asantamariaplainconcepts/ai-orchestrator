namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// What a command or query requires of its caller (BR-009: every operation names a required
/// permission). Declared on the request type and enforced by the pipeline, so a use case that
/// forgets cannot thereby become public (#13, design D1).
/// </summary>
public enum Access
{
    /// <summary>Any role on the project the request names. Observing and triggering (DEC-034).</summary>
    MemberOfProject = 1,

    /// <summary>Admin on the project the request names. Configuring, and anything destructive.</summary>
    AdminOfProject = 2,

    /// <summary>
    /// Reaches across projects, and narrows its own answer to the ones the caller may see. A
    /// separate value from <see cref="AnyCaller"/> even though the pipeline treats them the same,
    /// because the declaration is what a reviewer reads: collapsing them would make "I filter"
    /// indistinguishable from "I hand everything to everyone".
    /// </summary>
    FiltersToCaller = 3,

    /// <summary>
    /// Any caller, with nothing to scope it to. Creating the first project is the case: there is
    /// no project yet to hold a role on, and whoever creates one administers it (design D8).
    /// </summary>
    AnyCaller = 4,
}

/// <summary>
/// The declaration. Its absence is not "no requirement" — an undeclared operation is refused
/// outright, so the failure mode of forgetting is a refusal somebody notices rather than a hole
/// nobody does (design D1).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresAttribute(Access access) : Attribute
{
    public Access Access { get; } = access;
}

/// <summary>
/// A request that names the project it acts on. Paired with
/// <see cref="Access.MemberOfProject"/> or <see cref="Access.AdminOfProject"/>: the attribute says
/// what is required, this says where. Declaring one without the other is a programming error the
/// pipeline refuses loudly rather than guessing at.
/// </summary>
public interface IScopedToProject
{
    Guid ProjectId { get; }
}

/// <summary>
/// Thrown by the authorization decorator, rendered as 403 by the global handler. An exception
/// rather than an <c>ErrorOr</c> failure for the same reason validation is one: it short-circuits
/// a pipeline whose response type is generic, and every path out of it must be the same path.
/// <para>
/// The message names permission as the reason and says nothing else — not whether the project
/// exists, not what role would have been enough, not who holds it. A refusal that varies is a
/// refusal that answers questions (task 2.4).
/// </para>
/// </summary>
public sealed class PermissionDeniedException : Exception
{
    /// <summary>
    /// The whole message, for every refusal. Fixed rather than composed, which is why the
    /// constructors below take an operation and not a message: a caller who could word the refusal
    /// could word a revealing one.
    /// </summary>
    const string Refusal = "You do not have permission to perform this operation.";

    public PermissionDeniedException()
        : base(Refusal) { }

    public PermissionDeniedException(string operation)
        : base(Refusal) => Operation = operation;

    public PermissionDeniedException(string operation, Exception innerException)
        : base(Refusal, innerException) => Operation = operation;

    /// <summary>For the log, never for the response body.</summary>
    public string Operation { get; } = string.Empty;
}
