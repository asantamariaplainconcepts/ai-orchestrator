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

    /// <summary>
    /// This habitat cannot accept a pasted value (#124). Carries the store's own remedy, because
    /// what to do instead differs per habitat and the caller cannot know which one they are in.
    /// </summary>
    public static Error SecretStoreUnavailable(string detail) =>
        Error.Validation("Connector.SecretStoreUnavailable", detail);

    /// <summary>Exactly one of "here is the token" and "here is its name" (#124).</summary>
    public static Error CredentialInputAmbiguous(string detail) =>
        Error.Validation("Connector.CredentialInputAmbiguous", detail);

    /// <summary>Storing a credential is an Admin's act, and this caller is not one (#124, BR-009).</summary>
    public static Error NotPermitted(string action) =>
        Error.Forbidden("Connector.NotPermitted", $"Only an Admin can {action}.");

    public static Error SecretNotFound(string secretName) =>
        Error.Validation("Connector.SecretNotFound", $"No secret named '{secretName}' was found.");

    /// <summary>
    /// The vendor answered, and the answer was "this credential may not do that" (#132, design
    /// D3). Distinct from <see cref="VendorUnavailable"/>, which claims the vendor could not be
    /// reached — false whenever it replied, and the sentence that made a missing Contents
    /// permission take twenty minutes to diagnose. The vendor's own words travel with it because
    /// GitHub says "Resource not accessible by personal access token" and no paraphrase improves
    /// on that.
    /// </summary>
    public static Error PermissionRefused(string capability, string vendorReason) =>
        Error.Validation(
            "Connector.PermissionRefused",
            $"The vendor refused this credential for {capability}: {vendorReason}"
        );

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
