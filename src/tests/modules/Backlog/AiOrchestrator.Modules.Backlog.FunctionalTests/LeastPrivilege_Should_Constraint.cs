using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #226 — a Connector asks for the permissions its configuration will use, and proves it has
/// them. The three properties worth holding: writes are verified, a local code source does not
/// require the code ones, and verification still writes nothing.
/// </summary>
[Collection(BacklogCollection.Name)]
public class LeastPrivilege_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Configure(
        string codeSource = "Repository",
        string? localPath = null
    ) =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
                codeSource,
                localPath,
            }
        );

    [Fact]
    public async Task ACredentialThatCannotWrite_Should_BeRefusedAtSave()
    {
        // The failure this change exists to move: before, only the reads were probed, so this
        // credential was stored as verified and failed inside a Run instead.
        fixture.Vendor.WriteRefusal = BacklogErrors.CredentialRefused(
            "labelling and commenting on a Story",
            "the credential has read-only access to this repository"
        );

        var response = await Configure();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("labelling and commenting");
    }

    [Fact]
    public async Task ARepositoryCodeSource_Should_RequireThePublishCapability()
    {
        (await Configure()).EnsureSuccessStatusCode();

        fixture
            .Vendor.ProbedCapabilities.Select(capability => capability.Name)
            .ShouldContain("pushing a branch and opening a pull request");
    }

    [Fact]
    public async Task ALocalCodeSource_Should_NotRequireTheCodeCapabilities()
    {
        // The whole least-privilege point: a local folder's working copy is the host's own, so
        // nothing here pushes — asking for the permission would be asking for one nobody uses.
        var response = await Configure("LocalFolder", "/tmp/aio-least-privilege");

        // Whether the save succeeds depends on the habitat's self-host posture; what this asserts
        // is the capability set, which is computed before any of that.
        _ = response;

        fixture
            .Vendor.ProbedCapabilities.Select(capability => capability.Name)
            .ShouldNotContain("pushing a branch and opening a pull request");
    }

    [Fact]
    public async Task Verification_Should_WriteNothing()
    {
        (await Configure()).EnsureSuccessStatusCode();

        // The rule the spec already carried, re-asserted where the writes are now probed: a
        // probe that applied a label to find out would leave debris nobody consented to.
        fixture.Vendor.RepositoryLabels.ShouldBeEmpty();
        fixture.Vendor.Comments.ShouldBeEmpty();
    }
}
