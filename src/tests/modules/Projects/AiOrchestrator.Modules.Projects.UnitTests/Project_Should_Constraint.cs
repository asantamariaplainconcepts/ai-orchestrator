using AiOrchestrator.Modules.Projects.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

public class Project_Should_Constraint
{
    [Fact]
    public void Create_Should_AssignVersion7Identifier()
    {
        var project = Project.Create("Phoenix");

        project.Id.ShouldNotBe(Guid.Empty);
        project.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Create_Should_AssignTimeOrderedIdentifier()
    {
        var earlier = Project.Create("Earlier");
        var later = Project.Create("Later");

        // v7 stores a 48-bit big-endian timestamp in the leading bytes, which is what makes these
        // ids index well. Compare those bytes directly: Guid.CompareTo orders field-by-field and
        // does not reflect creation order. Two ids minted in the same millisecond tie, hence <= 0.
        var earlierTimestamp = earlier.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);
        var laterTimestamp = later.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);

        earlierTimestamp.SequenceCompareTo(laterTimestamp).ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public void Create_Should_KeepTheGivenName()
    {
        Project.Create("Phoenix").Name.ShouldBe("Phoenix");
    }
}
