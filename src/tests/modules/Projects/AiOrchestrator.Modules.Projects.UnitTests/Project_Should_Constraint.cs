using AiOrchestrator.Modules.Projects.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

public class Project_Should_Constraint
{
    [Fact]
    public void Create_Should_AssignTimeOrderedIdentifier()
    {
        var earlier = Project.Create("Earlier");
        var later = Project.Create("Later");

        // GUID v7 is time-ordered, which is why it indexes well as a primary key.
        earlier.Id.ShouldNotBe(Guid.Empty);
        later.Id.CompareTo(earlier.Id).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Create_Should_KeepTheGivenName()
    {
        Project.Create("Phoenix").Name.ShouldBe("Phoenix");
    }
}
