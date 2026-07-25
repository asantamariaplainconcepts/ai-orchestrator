using ErrorOr;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// The module's domain errors. Every failure path names one of these — call sites never
/// construct ad-hoc errors, so the API's problem codes stay a closed set.
/// </summary>
static class ProjectErrors
{
    public static Error NameAlreadyTaken(string name) =>
        Error.Conflict("Project.NameAlreadyTaken", $"A project named '{name}' already exists.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Project.NotFound", $"Project '{id}' was not found.");
}
