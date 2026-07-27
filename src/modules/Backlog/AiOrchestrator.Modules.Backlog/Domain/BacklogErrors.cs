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

    public static Error StoryNotFound(string vendorStoryId) =>
        Error.NotFound(
            "Backlog.StoryNotFound",
            $"The vendor has no story '{vendorStoryId}' in this repository."
        );

    /// <summary>Distinct from "no linked change" and "no documents" (design D5).</summary>
    public static Error DocumentNotFound(string path) =>
        Error.NotFound(
            "Backlog.DocumentNotFound",
            $"The document '{path}' could not be read from the linked change."
        );

    /// <summary>The vendor's state vocabulary is finite; naming what was refused saves a guess.</summary>
    public static Error StateNotAccepted(string state, string accepted) =>
        Error.Validation(
            "Backlog.StateNotAccepted",
            $"The vendor does not accept the state '{state}'. Accepted: {accepted}."
        );

    public static Error ConnectorNotFound(Guid projectId) =>
        Error.NotFound("Connector.NotFound", $"Project '{projectId}' has no connector.");
}
