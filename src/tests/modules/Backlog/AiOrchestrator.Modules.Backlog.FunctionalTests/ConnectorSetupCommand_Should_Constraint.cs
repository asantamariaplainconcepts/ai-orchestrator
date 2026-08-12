using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #332 — the setup command as configuration: stored beside the folder, blank meaning absent, and
/// cleared with the code source that made it applicable. The self-host posture comes from a derived
/// factory carrying <c>Identity:Mode=LocalOwner</c>, the way the local code-source surface is
/// composed in the product, rather than by faking the check.
/// </summary>
[Collection(BacklogCollection.Name)]
public class ConnectorSetupCommand_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    WebApplicationFactory<Program>? _selfHost;
    HttpClient _client = null!;
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();

        _selfHost = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Mode", "LocalOwner")
        );
        _client = _selfHost.CreateClient();
    }

    public Task DisposeAsync()
    {
        _selfHost?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ALocalFolderConnector_Should_StoreAndReturnItsSetupCommand()
    {
        var stored = await Configure(
            codeSource: "LocalFolder",
            localPath: "/tmp/aio-setup-command",
            localSetupCommand: "pnpm install --frozen-lockfile && pnpm build"
        );

        stored
            .GetProperty("localSetupCommand")
            .GetString()
            .ShouldBe("pnpm install --frozen-lockfile && pnpm build");

        // The coordinates and the credential are untouched by the new field.
        stored.GetProperty("owner").GetString().ShouldBe("acme");
        stored.GetProperty("localPath").GetString().ShouldBe("/tmp/aio-setup-command");
    }

    [Fact]
    public async Task ABlankCommand_Should_BeStoredAsAbsent_NotAsAnEmptyString()
    {
        var stored = await Configure(
            codeSource: "LocalFolder",
            localPath: "/tmp/aio-setup-command",
            localSetupCommand: "   "
        );

        // One stored value means one thing: null is "nothing to prepare", which is a valid
        // configuration rather than a misconfigured one.
        stored.GetProperty("localSetupCommand").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task AConnectorConfiguredWithoutOne_Should_ReadAsHavingNone()
    {
        // The same observable fact the additive migration gives every Connector written before
        // this change: the column is null and nothing about the Connector behaves differently.
        var stored = await Configure(
            codeSource: "LocalFolder",
            localPath: "/tmp/aio-setup-command",
            localSetupCommand: null
        );

        stored.GetProperty("localSetupCommand").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SwitchingBackToTheRepositorySource_Should_ClearTheStoredCommand()
    {
        await Configure(
            codeSource: "LocalFolder",
            localPath: "/tmp/aio-setup-command",
            localSetupCommand: "pnpm install"
        );

        // Hiding and clearing are the same act. A command that survived this would be
        // configuration nobody can see — and a later switch back to the folder would run it.
        var repository = await Configure(
            codeSource: "Repository",
            localPath: null,
            localSetupCommand: null
        );

        repository.GetProperty("localSetupCommand").ValueKind.ShouldBe(JsonValueKind.Null);
        repository.GetProperty("localPath").ValueKind.ShouldBe(JsonValueKind.Null);

        // And it stays cleared when the folder comes back, rather than reappearing.
        var backToLocal = await Configure(
            codeSource: "LocalFolder",
            localPath: "/tmp/aio-setup-command",
            localSetupCommand: null
        );

        backToLocal.GetProperty("localSetupCommand").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    async Task<JsonElement> Configure(
        string codeSource,
        string? localPath,
        string? localSetupCommand
    )
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
                codeSource,
                localPath,
                localSetupCommand,
            }
        );
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
