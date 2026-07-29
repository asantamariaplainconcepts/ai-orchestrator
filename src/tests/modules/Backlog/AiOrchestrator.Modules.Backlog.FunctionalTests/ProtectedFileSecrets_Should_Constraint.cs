using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #124 — the self-host habitat's store. What must hold: what goes in comes back out, what sits
/// on disk is not the token, and a deployment that already keeps its PAT in the environment is
/// not broken by this existing.
/// <para>
/// No fixture and no containers: this is the store on its own, which is the only way to assert
/// what is actually written to disk.
/// </para>
/// </summary>
public class ProtectedFileSecrets_Should_Constraint : IDisposable
{
    const string Token = "github_pat_11ABCDE_thisisthesecretvalue";

    readonly string _root = Path.Combine(Path.GetTempPath(), $"aio-secrets-{Guid.NewGuid():N}");
    readonly IDataProtector _protector;

    public ProtectedFileSecrets_Should_Constraint() =>
        // The same primitive the host composes — a test that hand-rolled protection would be
        // asserting something the product does not do.
        _protector = DataProtectionProvider
            .Create(new DirectoryInfo(Path.Combine(_root, "keys")))
            .CreateProtector("AiOrchestrator.Secrets.v1");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    string Values => Path.Combine(_root, "values");

    ProtectedFileSecretStore Store() => new(_protector, Values);

    ProtectedFileSecretResolver Resolver(params (string Key, string Value)[] configuration) =>
        new(
            _protector,
            Values,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configuration.Select(entry => new KeyValuePair<string, string?>(
                        entry.Key,
                        entry.Value
                    ))
                )
                .Build()
        );

    [Fact]
    public async Task AStoredValue_Should_ResolveBack()
    {
        await Store().Store("connector-github-abc", Token);

        (await Resolver().Resolve("connector-github-abc")).ShouldBe(Token);
    }

    [Fact]
    public async Task WhatIsOnDisk_Should_NotBeTheToken()
    {
        await Store().Store("connector-github-abc", Token);

        var written = Directory.GetFiles(Values).ShouldHaveSingleItem();
        var contents = await File.ReadAllTextAsync(written);

        contents.ShouldNotContain(Token);
        contents.ShouldNotContain("thisisthesecretvalue");

        // Nor is the name recoverable from the path — hashed, so no filesystem has to accept
        // whatever characters a vendor allows in a secret name.
        Path.GetFileName(written).ShouldNotContain("connector-github-abc");
    }

    [Fact]
    public async Task AValueWrittenTwice_Should_KeepOneFileAndTheLastValue()
    {
        await Store().Store("connector-github-abc", Token);
        await Store().Store("connector-github-abc", "rotated");

        Directory.GetFiles(Values).Length.ShouldBe(1);
        (await Resolver().Resolve("connector-github-abc")).ShouldBe("rotated");
    }

    [Fact]
    public async Task AnEnvironmentSuppliedSecret_Should_StillResolve()
    {
        // The self-hoster who already set Secrets__acme-pat keeps working, unmigrated.
        var resolver = Resolver(("Secrets:acme-pat", "from-the-environment"));

        (await resolver.Resolve("acme-pat")).ShouldBe("from-the-environment");
    }

    [Fact]
    public async Task AStoredValue_Should_WinOverTheEnvironment()
    {
        await Store().Store("acme-pat", "from-the-store");
        var resolver = Resolver(("Secrets:acme-pat", "from-the-environment"));

        (await resolver.Resolve("acme-pat")).ShouldBe("from-the-store");
    }

    [Fact]
    public async Task AnUnknownName_Should_SayItIsMissing()
    {
        await Should.ThrowAsync<SecretNotFoundException>(() => Resolver().Resolve("never-stored"));
    }

    [Fact]
    public async Task AValueWrittenWithAnotherKeyRing_Should_SayThatRatherThanNotFound()
    {
        await Store().Store("connector-github-abc", Token);

        // A deployment that lost its key ring: the file is right there, and "not found" would
        // send the reader looking for a secret that exists.
        var stranger = new ProtectedFileSecretResolver(
            DataProtectionProvider
                .Create(new DirectoryInfo(Path.Combine(_root, "other-keys")))
                .CreateProtector("AiOrchestrator.Secrets.v1"),
            Values,
            new ConfigurationBuilder().Build()
        );

        var failure = await Should.ThrowAsync<SecretNotFoundException>(() =>
            stranger.Resolve("connector-github-abc")
        );
        failure.Message.ShouldContain("cannot be decrypted");
    }
}
