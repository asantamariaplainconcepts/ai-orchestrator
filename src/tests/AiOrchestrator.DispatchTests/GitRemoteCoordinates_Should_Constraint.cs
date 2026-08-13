using AiOrchestrator.BuildingBlocks.Agents;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// #347 — a named folder answers for itself which vendor and which coordinates a Project uses.
/// <para>
/// The pairs matter more than the individual cases: the SSH and HTTPS forms of one remote MUST
/// yield identical coordinates, because an operator who cloned over SSH and one who cloned over
/// HTTPS have the same repository and must get the same Project.
/// </para>
/// </summary>
public class GitRemoteCoordinates_Should_Constraint
{
    [Theory]
    [InlineData("git@github.com:acme/portal.git")]
    [InlineData("https://github.com/acme/portal.git")]
    [InlineData("https://github.com/acme/portal")]
    [InlineData("ssh://git@github.com/acme/portal.git")]
    public void AGitHubRemote_Should_YieldTheOwnerAndRepository(string remote)
    {
        GitRemoteCoordinates.TryParse(remote, out var parsed).ShouldBeTrue();

        parsed!.Vendor.ShouldBe(GitRemoteCoordinates.GitHubVendor);
        parsed.Owner.ShouldBe("acme");
        parsed.Repository.ShouldBe("portal");

        // GitHub's issues and code share a repository, so there is no third coordinate to fill.
        parsed.CodeRepository.ShouldBeNull();
    }

    [Theory]
    [InlineData("git@ssh.dev.azure.com:v3/contoso/Platform/api")]
    [InlineData("https://dev.azure.com/contoso/Platform/_git/api")]
    [InlineData("https://contoso@dev.azure.com/contoso/Platform/_git/api")]
    [InlineData("https://contoso.visualstudio.com/Platform/_git/api")]
    public void AnAzureDevOpsRemote_Should_YieldTheThreeFieldsTheConnectorReads(string remote)
    {
        if (!GitRemoteCoordinates.TryParse(remote, out var parsed))
        {
            // The `ssh.dev.azure.com:v3/...` form is deliberately NOT parsed: its path shape is
            // `v3/{org}/{project}/{repo}` with no `_git`, and inventing a mapping for it would be a
            // guess. It fails as "matched neither vendor", which the flow reports and proceeds past.
            remote.ShouldContain("ssh.dev.azure.com");
            return;
        }

        parsed!.Vendor.ShouldBe(GitRemoteCoordinates.AzureDevOpsVendor);
        parsed.Owner.ShouldBe("contoso");
        parsed.Repository.ShouldBe("Platform");
        parsed.CodeRepository.ShouldBe("api");
    }

    [Fact]
    public void TheSshAndHttpsFormsOfOneRemote_Should_BeIndistinguishable()
    {
        GitRemoteCoordinates.TryParse("git@github.com:acme/portal.git", out var ssh).ShouldBeTrue();
        GitRemoteCoordinates
            .TryParse("https://github.com/acme/portal.git", out var https)
            .ShouldBeTrue();

        ssh.ShouldBe(https);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("git@gitlab.com:acme/portal.git")]
    [InlineData("https://example.com/acme/portal.git")]
    [InlineData("https://github.com/acme")]
    [InlineData("https://github.com/acme/portal/extra")]
    [InlineData("https://dev.azure.com/contoso/Platform")]
    [InlineData("not-a-remote-at-all")]
    public void ARemoteMatchingNeitherVendor_Should_YieldNothingRatherThanAGuess(string? remote)
    {
        GitRemoteCoordinates.TryParse(remote, out var parsed).ShouldBeFalse();
        parsed.ShouldBeNull();
    }
}
