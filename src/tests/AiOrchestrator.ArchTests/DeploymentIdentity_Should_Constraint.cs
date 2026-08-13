using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// The first of #119's two locks, asserted against the infrastructure definition rather than
/// assumed: Terraform never sets the local-owner identity. The second lock — the host refusing
/// to start — is in <see cref="LocalOwnerIdentity_Should_Constraint"/>, and exists precisely
/// because this one cannot survive somebody editing the Azure portal by hand.
/// </summary>
public class DeploymentIdentity_Should_Constraint
{
    [Fact]
    public void Terraform_Should_NeverConfigureTheLocalOwner()
    {
        var infrastructure = Directory.GetFiles(
            Path.Combine(RepositoryRoot(), "infra"),
            "*.tf",
            SearchOption.AllDirectories
        );

        infrastructure.ShouldNotBeEmpty("the infrastructure definition was not found");

        foreach (var file in infrastructure)
        {
            File.ReadAllText(file)
                .ShouldNotContain(
                    "Identity__Mode",
                    Case.Insensitive,
                    $"{Path.GetFileName(file)} configures the local owner. It grants the Admin "
                        + "role with no sign-in, so it belongs only on a machine one person owns."
                );
        }
    }

    static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !IsRepositoryRoot(directory))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("the repository root was not found from the test's location");
        return directory.FullName;
    }

    /// <summary>
    /// Whether <paramref name="directory"/> is the root of a checkout — <c>.git</c> as a directory in
    /// an ordinary clone, or as a <b>file</b> in a git worktree, where it holds a
    /// <c>gitdir: …</c> pointer instead.
    /// <para>
    /// The file form is why this exists. Testing only for a directory walked past every worktree root
    /// to the filesystem root, so the suite failed with "the repository root was not found" on every
    /// local full run in a worktree while staying green in CI, which clones. A test that is red
    /// locally and green remotely teaches its reader to ignore a red suite, which is the real cost.
    /// <see cref="AiOrchestrator.ServiceDefaults.Agents.LocalCheckoutReaper"/> already reads the
    /// pointer file for the same reason; this only needs to know a root when it sees one.
    /// </para>
    /// </summary>
    static bool IsRepositoryRoot(DirectoryInfo directory)
    {
        var dotGit = Path.Combine(directory.FullName, ".git");
        return Directory.Exists(dotGit) || File.Exists(dotGit);
    }
}
