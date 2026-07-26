using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.ServiceDefaults.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// The composition rule the deployed environment depends on: which resolver a host gets is
/// decided by whether a vault URI is configured — never by the environment name.
/// <para>
/// That distinction is not academic. The migration bootstrap shipped a gate on
/// <c>IsProduction()</c> that guessed wrong under `aspire run` and silently skipped, and the
/// symptom was a database with no schema. A key is present or it is not; there is nothing left
/// to infer, and this test is what keeps it that way.
/// </para>
/// </summary>
public class SecretResolution_Should_Constraint
{
    [Fact]
    public void Resolution_Should_UseConfigurationWithoutAVaultUri()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddSecretResolution();

        Resolve(builder).ShouldBeOfType<ConfigurationSecretResolver>();
    }

    [Fact]
    public void Resolution_Should_UseKeyVaultWhenAVaultUriIsSet()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[SecretResolution.KeyVaultUriKey] =
            "https://kv-aio-dev.vault.azure.net/";

        builder.AddSecretResolution();

        // Resolving proves the Aspire client integration actually registered a SecretClient the
        // resolver can take — a registration that compiles but cannot be constructed would pass
        // a weaker assertion on the service descriptor alone.
        Resolve(builder).ShouldBeOfType<KeyVaultSecretResolver>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolution_Should_TreatABlankVaultUriAsAbsent(string configured)
    {
        // A blank value is what an unset environment variable looks like in a container; reading
        // it as "use Key Vault" would fail at startup with a URI parse error instead of falling
        // back to the only store that exists.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[SecretResolution.KeyVaultUriKey] = configured;

        builder.AddSecretResolution();

        Resolve(builder).ShouldBeOfType<ConfigurationSecretResolver>();
    }

    static ISecretResolver Resolve(HostApplicationBuilder builder) =>
        builder.Build().Services.GetRequiredService<ISecretResolver>();
}
