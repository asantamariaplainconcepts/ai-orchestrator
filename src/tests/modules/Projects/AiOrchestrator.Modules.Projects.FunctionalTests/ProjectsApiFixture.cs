using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// One container stack for the whole Projects module — shared through
/// <see cref="ProjectsCollection"/>, because a stack per test class overwhelms the runner.
/// </summary>
public sealed class ProjectsApiFixture : ApiServiceFixtureBase
{
    protected override string[] SchemasToReset => [ProjectsDbContext.Schema];
}

[CollectionDefinition(Name)]
public sealed class ProjectsCollection : ICollectionFixture<ProjectsApiFixture>
{
    public const string Name = "Projects";
}
