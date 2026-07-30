namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// What a command or query requires of its caller. Declared on the request type and enforced by the
/// shared pipeline, so a use case that forgets cannot thereby become public (#13, design D1).
/// <para>
/// Two shapes, because there are two kinds of operation. Most name a <b>permission</b> and act on a
/// project, which is BR-009 exactly: <i>every operation names a required permission; roles are
/// permission bundles</i>. A few name neither because no permission could describe them — a
/// cross-project list, or creating the first project. Those say so explicitly; what none of them may
/// do is say nothing.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresAttribute : Attribute
{
    /// <summary>
    /// The permission required, on the project the request names. Checked against the caller's
    /// bundle through <see cref="PermissionGrants"/> — never against a role written into a handler,
    /// which is what makes a new bundle a change to one table instead of a sweep over every
    /// declaration (DEC-034's "custom roles post-MVP").
    /// </summary>
    public RequiresAttribute(string permission) => Permission = permission;

    /// <summary>For the operations no permission can describe. See <see cref="Identity.Access"/>.</summary>
    public RequiresAttribute(Access access) => Access = access;

    public string? Permission { get; }

    public Access? Access { get; }
}

/// <summary>
/// The declarations that are not permissions. Deliberately few, deliberately named: each one is a
/// claim a reviewer can check, and "nothing to declare" is not among them.
/// </summary>
public enum Access
{
    /// <summary>
    /// Reaches across projects, and narrows its own answer to the ones the caller may see. A named
    /// value rather than an omission, because the declaration is what a reviewer reads: without it,
    /// "I filter" would be indistinguishable from "I hand everything to everyone".
    /// </summary>
    FiltersToCaller = 1,

    /// <summary>
    /// Any caller, with nothing to scope it to. Creating the first project is the case: there is no
    /// project yet to hold a permission on, and whoever creates one administers it (design D8).
    /// </summary>
    AnyCaller = 2,
}

/// <summary>
/// A request that names the project it acts on. Every permission here is project-scoped — the
/// project is this product's only tenant — so declaring a permission without this is a wiring
/// mistake the pipeline refuses loudly rather than guessing at.
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
/// exists, not what would have been enough, not who holds it. A refusal that varies is a refusal
/// that answers questions (task 2.4).
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
