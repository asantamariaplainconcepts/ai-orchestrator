using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Domain;

/// <summary>
/// The module's closed set of domain errors. Call sites never construct errors ad hoc, so the
/// API's problem codes stay finite and mean the same thing everywhere.
/// </summary>
static class BacklogErrors
{
    /// <summary>The coordinates are wrong — repository missing, or not visible to this credential.</summary>
    public static Error RepositoryNotFound(string owner, string repository) =>
        Error.Validation(
            "Connector.RepositoryNotFound",
            $"Repository '{owner}/{repository}' was not found, or the credential cannot see it."
        );

    /// <summary>The credential is wrong — distinct from the above, because the fix is different.</summary>
    public static Error CredentialRejected(string secretName) =>
        Error.Validation(
            "Connector.CredentialRejected",
            $"The vendor rejected the credential in secret '{secretName}'."
        );

    public static Error SecretNotFound(string secretName) =>
        Error.Validation("Connector.SecretNotFound", $"No secret named '{secretName}' was found.");

    public static Error VendorUnavailable(string detail) =>
        Error.Failure("Connector.VendorUnavailable", $"The vendor could not be reached: {detail}");

    public static Error ConnectorNotFound(Guid projectId) =>
        Error.NotFound("Connector.NotFound", $"Project '{projectId}' has no connector.");
}
