using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.ServiceDefaults.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// #119 — who the caller is, per habitat, and the lock that keeps the local owner off provisioned
/// infrastructure. Composition-level because that is where the decision is made: no database, no
/// web host, just the builder and its configuration.
/// </summary>
public class LocalOwnerIdentity_Should_Constraint
{
    static IHostApplicationBuilder Builder(params (string Key, string Value)[] settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            settings.Select(setting => new KeyValuePair<string, string?>(
                setting.Key,
                setting.Value
            ))
        );
        return builder;
    }

    [Fact]
    public void ALocalHabitat_Should_HaveAnOwner()
    {
        var builder = Builder((IdentityComposition.ModeKey, "LocalOwner"));

        builder.AddIdentity();
        var principal = builder
            .Services.BuildServiceProvider()
            .GetRequiredService<ICurrentPrincipal>();

        // No role asserted here any more (#13): the principal answers who, and what the owner may
        // do is IProjectPermissions — which the Projects module composes, and which
        // ProjectRoles_Should_Constraint covers.
        principal.Current.Id.ShouldBe(Principal.LocalOwnerId);
        principal.Current.DisplayName.ShouldBe("Local owner");
    }

    [Fact]
    public void TheOwnersName_Should_BeTheirs()
    {
        var builder = Builder(
            (IdentityComposition.ModeKey, "LocalOwner"),
            (IdentityComposition.OwnerNameKey, "Andoni")
        );

        builder.AddIdentity();

        builder
            .Services.BuildServiceProvider()
            .GetRequiredService<ICurrentPrincipal>()
            .Current.DisplayName.ShouldBe("Andoni");
    }

    [Fact]
    public void ProvisionedInfrastructure_Should_RefuseTheLocalOwner()
    {
        // The lock: a managed secret store means Terraform provisioned this, and provisioned
        // infrastructure is not a machine somebody owns.
        var builder = Builder(
            (IdentityComposition.ModeKey, "LocalOwner"),
            ("Secrets:KeyVaultUri", "https://example.vault.azure.net/")
        );

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddIdentity());

        refusal.Message.ShouldContain("managed secret store");
    }

    [Fact]
    public void APublicAddress_Should_RefuseTheLocalOwner()
    {
        var builder = Builder(
            (IdentityComposition.ModeKey, "LocalOwner"),
            ("ASPNETCORE_URLS", "http://aio.example.com:8080")
        );

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddIdentity());

        refusal.Message.ShouldContain("not loopback");
    }

    [Fact]
    public void AContainersWildcardBind_Should_NotRefuse()
    {
        // The false positive that would have broken the self-host compose: every container binds
        // every interface, so the bind cannot be the signal (design D2).
        var builder = Builder(
            (IdentityComposition.ModeKey, "LocalOwner"),
            ("ASPNETCORE_URLS", "http://+:8080")
        );

        Should.NotThrow(() => builder.AddIdentity());
    }

    [Fact]
    public void AProductionEnvironmentAlone_Should_NotRefuse()
    {
        // The other false positive, and the one that actually shipped for a moment: the
        // self-host compose sets no ASPNETCORE_ENVIRONMENT, so ASP.NET calls it Production.
        // Gating on the environment name refused to start the habitat DEC-049 protects.
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = Environments.Production }
        );
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(IdentityComposition.ModeKey, "LocalOwner"),
        ]);

        Should.NotThrow(() => builder.AddIdentity());
    }

    [Fact]
    public void NoIdentityConfigured_Should_StillYieldAPrincipal()
    {
        // Design D1: there is no null case. A hosted deployment without a provider has a
        // principal too — the state is announced at startup, not expressed as an absence
        // every call site has to handle.
        var builder = Builder();

        builder.AddIdentity();

        builder
            .Services.BuildServiceProvider()
            .GetRequiredService<ICurrentPrincipal>()
            .Current.DisplayName.ShouldBe("Not signed in");
    }
}
