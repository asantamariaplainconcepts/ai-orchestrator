using AiOrchestrator.Modules.Backlog.Features.Backlog;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.UnitTests;

/// <summary>
/// #150 — resolving a prompt name against the project's directory. Unit-tested because it is the one
/// place a path is composed, and what a refusal names depends on it being composed the same way every
/// time.
/// </summary>
public class PromptPath_Should_Constraint
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnUnsetDirectory_Should_MeanTheConvention(string? directory)
    {
        var (path, failure) = PromptPath.Resolve(directory, "estimate.md");

        failure.ShouldBeNull();
        path.ShouldBe("ai/prompts/estimate.md");
    }

    [Theory]
    [InlineData("prompts/ours")]
    [InlineData("/prompts/ours")]
    [InlineData("prompts/ours/")]
    [InlineData("/prompts/ours/")]
    public void SlashesAroundTheDirectory_Should_NotChangeWhatItMeans(string directory)
    {
        // One composition rule, so the stored value's punctuation cannot produce 'a//b'.
        PromptPath.Resolve(directory, "estimate.md").Path.ShouldBe("prompts/ours/estimate.md");
    }

    [Fact]
    public void ASubfolder_Should_BeAllowed()
    {
        PromptPath.Resolve(null, "deep/estimate.md").Path.ShouldBe("ai/prompts/deep/estimate.md");
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("\\etc\\passwd")]
    public void AnAbsoluteName_Should_BeRefused(string name)
    {
        var (path, failure) = PromptPath.Resolve(null, name);

        path.ShouldBeNull();
        failure.ShouldNotBeNull();
        failure.ShouldContain("absolute");
    }

    [Theory]
    [InlineData("../secrets.md")]
    [InlineData("deep/../../secrets.md")]
    [InlineData("deep\\..\\..\\secrets.md")]
    public void ANameThatClimbsOut_Should_BeRefused(string name)
    {
        // Refused rather than normalized: the directory exists to bound where prompts come from, and
        // a boundary that can be stepped over bounds nothing. Backslashes count, because a
        // '/'-only check would let '..\' through.
        var (path, failure) = PromptPath.Resolve(null, name);

        path.ShouldBeNull();
        failure.ShouldNotBeNull();
        failure.ShouldContain("leaves the prompts directory");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoName_Should_BeRefusedRatherThanResolvedToTheDirectory(string? name)
    {
        var (path, failure) = PromptPath.Resolve(null, name);

        path.ShouldBeNull();
        failure.ShouldNotBeNull();
    }
}
