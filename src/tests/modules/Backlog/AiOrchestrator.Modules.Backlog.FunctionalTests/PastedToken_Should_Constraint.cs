using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #124 — connecting a backlog by pasting the token. What must hold: the value goes to the
/// habitat's store under a name the product chose, it never comes back out by any route, and the
/// path that names an existing secret behaves exactly as it did before this existed.
/// </summary>
[Collection(BacklogCollection.Name)]
public class PastedToken_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    const string Token = "github_pat_11ABCDE_thisisthesecretvalue";

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Secrets.Reset();
        fixture.Caller.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Configure(object body) =>
        _client.PutAsJsonAsync($"/api/projects/{_projectId}/connector", body);

    Task<HttpResponseMessage> Paste(string token = Token) =>
        Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                accessToken = token,
            }
        );

    async Task<(string SecretName, DateTimeOffset? SetAt)> StoredConnector()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        var connector = await database.Connectors.SingleAsync(entity =>
            entity.ProjectId == _projectId
        );
        return (connector.SecretName, connector.SecretSetAt);
    }

    [Fact]
    public async Task APastedToken_Should_BeStoredUnderANameTheProductChose()
    {
        var response = await Paste();
        response.EnsureSuccessStatusCode();

        var (secretName, setAt) = await StoredConnector();

        // Derived from the project, so it cannot collide and rotation overwrites (design D2).
        secretName.ShouldBe($"connector-github-{_projectId:N}");
        setAt.ShouldNotBeNull();

        fixture.Secrets.Stored[secretName].ShouldBe(Token);
    }

    [Fact]
    public async Task ThePastedValue_Should_NotAppearInTheResponse()
    {
        var response = await Paste();
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(Token);

        // Not even a fragment: a truncated credential in a log is still a credential.
        body.ShouldNotContain("thisisthesecretvalue");
    }

    [Fact]
    public async Task ThePastedValue_Should_NotAppearWhenTheConnectorIsReadBack()
    {
        (await Paste()).EnsureSuccessStatusCode();

        var body = await _client.GetStringAsync($"/api/projects/{_projectId}/backlog");

        body.ShouldNotContain(Token);
        body.ShouldContain($"connector-github-{_projectId:N}");
    }

    [Fact]
    public async Task APastedToken_Should_BeVerifiedWithTheValueThatWasStored()
    {
        (await Paste()).EnsureSuccessStatusCode();

        // The round trip is the point of design D3: the vendor saw what the store handed back,
        // not what the request carried, so a store that dropped the write is caught here.
        fixture.Vendor.VerifiedToken.ShouldBe(Token);
    }

    [Fact]
    public async Task Rotation_Should_ReplaceTheValueUnderTheSameName()
    {
        (await Paste()).EnsureSuccessStatusCode();
        (await Paste("github_pat_22FGHIJ_therotatedvalue")).EnsureSuccessStatusCode();

        var (secretName, _) = await StoredConnector();
        fixture.Secrets.Stored[secretName].ShouldBe("github_pat_22FGHIJ_therotatedvalue");

        // One name, one entry: rotation leaves no orphan for anybody to clean up.
        fixture.Secrets.Stored.Count.ShouldBe(1);
    }

    [Fact]
    public async Task NamingAnExistingSecret_Should_BehaveExactlyAsBefore()
    {
        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
            }
        );
        response.EnsureSuccessStatusCode();

        var (secretName, setAt) = await StoredConnector();
        secretName.ShouldBe("acme-pat");

        // Null, not a timestamp: the product did not write this one and does not know when
        // somebody else did. "Never" would be a lie; absence is the truth.
        setAt.ShouldBeNull();
        fixture.Secrets.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task NeitherCredential_Should_BeRefused()
    {
        var response = await Configure(new { owner = "acme", repository = "portal" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("not both and not neither");
    }

    [Fact]
    public async Task BothCredentials_Should_BeRefused()
    {
        var response = await Configure(
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
                accessToken = Token,
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AHabitatThatCannotStore_Should_RefuseNamingTheRemedy()
    {
        fixture.Secrets.UnavailableRemedy = "Add Secrets__<name> to the environment.";

        var response = await Paste();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(
            "Add Secrets__<name> to the environment."
        );

        // And the other path still works there — the refusal is about storing, not connecting.
        (
            await Configure(
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ACallerWhoIsNotAnAdmin_Should_BeRefusedAndStoreNothing()
    {
        fixture.Caller.Current = new("someone", "A member", PrincipalRole.Member);

        var response = await Paste();

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        fixture.Secrets.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task AVendorRefusal_Should_LeaveNoConnector()
    {
        fixture.Vendor.VerifyError = Domain.BacklogErrors.CredentialRejected("(supplied)");

        var response = await Paste();
        response.IsSuccessStatusCode.ShouldBeFalse();

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        (
            await database.Connectors.AnyAsync(entity => entity.ProjectId == _projectId)
        ).ShouldBeFalse();

        // The stored value survives, deliberately: it is inert without a Connector, and the
        // derived name means the next attempt overwrites it rather than accumulating (design D3).
        fixture.Secrets.Stored.ShouldNotBeEmpty();
    }
}
