using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #250 — which declaration set run mode applies is a parameter with a default. What is asserted
/// is the composition itself (the ADR-0004 lesson): the environment each habitat would hand the
/// Server, read from the model without booting anything — booting is the shared fixture's job,
/// and it already proves the default habitat runs.
/// </summary>
[Trait("Category", "E2E")]
public class HabitatParameter_Should_Constraint
{
    static async Task<Dictionary<string, string>> ServerEnvironment(string? habitat)
    {
        // As an argument, because the AppHost's Program reads the parameter while CreateAsync
        // runs it — configuration set on the returned builder arrives after the choice is made.
        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AiOrchestrator_AppHost>(
                habitat is null ? [] : [$"--Parameters:habitat={habitat}"]
            );

        var server = builder.Resources.Single(resource => resource.Name == "server");
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            server,
            cancellationToken: CancellationToken.None
        );

        foreach (var callback in server.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }

        return context
            .EnvironmentVariables.Where(entry => entry.Value is string)
            .ToDictionary(entry => entry.Key, entry => (string)entry.Value);
    }

    [Fact]
    public async Task NothingConfigured_Should_BeTheDevLoop()
    {
        var environment = await ServerEnvironment(habitat: null);

        // The dev loop, exactly as before #250: seeder on, store paths set, no pod image and
        // no locus reason — the declarations that make the first `aspire run` clickable.
        environment["LocalLoop:Seed"].ShouldBe("true");
        environment["Identity__Mode"].ShouldBe("LocalOwner");
        environment.ShouldContainKey("Secrets__LocalStorePath");
        environment.ShouldNotContainKey("Dispatch__PodImage");
        environment.ShouldNotContainKey("Habitat__LocalFolderUnavailableReason");
    }

    [Fact]
    public async Task Postgres_Should_UseThePersistedSecretPassword()
    {
        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AiOrchestrator_AppHost>();
        var postgres = builder.Resources.OfType<PostgresServerResource>().Single();

        // The named secret is the other half of the persistent data volume: Aspire writes its
        // generated value to AppHost user secrets in run mode, so a later composition supplies
        // the hash already stored in PostgreSQL instead of generating a mismatched password.
        postgres.PasswordParameter.Name.ShouldBe("postgres-password");
        postgres.PasswordParameter.Secret.ShouldBeTrue();
        postgres.PasswordParameter.Default!.GetType().Name.ShouldBe("UserSecretsParameterDefault");
    }

    [Fact]
    public async Task TheServerHabitat_Should_CarryTheComposeDeclarations()
    {
        var environment = await ServerEnvironment("server");

        // The same set the generated compose carries (#246, #247) — one method, both routes,
        // so the rehearsal and the artifact cannot drift.
        environment["Identity__Mode"].ShouldBe("LocalOwner");
        // A per-Run microVM since #296, where it used to be a container launched over the docker
        // socket. The key changing is the whole point: an operator's compose no longer grants a
        // socket to anything.
        environment["Agents__Sandbox__Launcher"].ShouldBe("sbx");
        environment.ShouldNotContainKey("Dispatch__PodImage");
        environment["Habitat__LocalFolderUnavailableReason"].ShouldContain("container");
        // …and none of the dev loop's: an operator's first boot has no demo project.
        environment.ShouldNotContainKey("LocalLoop:Seed");
        environment.ShouldNotContainKey("Secrets__LocalStorePath");
    }

    [Fact]
    public async Task AnUnknownHabitat_Should_RefuseNamingBoth()
    {
        var refusal = await Should.ThrowAsync<InvalidOperationException>(() =>
            ServerEnvironment("cloud")
        );

        refusal.Message.ShouldContain("'cloud'");
        refusal.Message.ShouldContain("'local'");
        refusal.Message.ShouldContain("'server'");
    }
}
