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

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("the repository root was not found from the test's location");
        return directory.FullName;
    }
}
