using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #13 / UC-002 / BR-009 — roles are per project, and every operation names what it requires.
/// <para>
/// Run in the habitat that actually has roles: <c>AzureAd:ClientId</c> is set, so the module
/// composes the reader that consults rows rather than the one for a machine one person owns. Every
/// other functional test runs in the second habitat, where every caller is the owner — which is
/// correct there and would prove nothing here.
/// </para>
/// <para>
/// Callers are authenticated by a test scheme reading an object id from a header, so one client can
/// act as several people. What that replaces is the provider, not the product: the claim it writes
/// is the one Entra writes, and everything below it — <c>SignedInCaller</c>, the role rows, the
/// decorator — is the composed article.
/// </para>
/// </summary>
[Collection(ProjectsCollection.Name)]
public class ProjectRoleAssignment_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    const string Owner = "11111111-1111-1111-1111-111111111111";
    const string Member = "22222222-2222-2222-2222-222222222222";
    const string Stranger = "33333333-3333-3333-3333-333333333333";
    const string NeverSeen = "99999999-9999-9999-9999-999999999999";

    public Task InitializeAsync() => fixture.ResetDatabase();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A host where people sign in. <paramref name="bootstrap"/> is design D4's list.</summary>
    HttpClient Client(string? bootstrap = null) =>
        fixture
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000001");
                builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000002");
                if (bootstrap is not null)
                {
                    builder.UseSetting("Auth:BootstrapAdmins", bootstrap);
                }

                builder.ConfigureTestServices(services =>
                {
                    services
                        .AddAuthentication()
                        .AddScheme<AuthenticationSchemeOptions, HeaderCaller>("Test", _ => { });

                    // The provider's scheme stays registered and unused: what the pipeline reads is
                    // the default authenticate scheme, and pointing that here is the whole swap.
                    services.PostConfigure<AuthenticationOptions>(options =>
                        options.DefaultAuthenticateScheme = "Test"
                    );
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    static HttpRequestMessage As(
        string identityId,
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(HeaderCaller.Header, identityId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    static async Task<Guid> CreateProject(HttpClient client, string identityId)
    {
        var response = await client.SendAsync(
            As(identityId, HttpMethod.Post, "/api/projects", new { name = $"p-{Guid.NewGuid():N}" })
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
    }

    /// <summary>Signing in once is what makes somebody grantable (task 4.1) — /api/me records it.</summary>
    static async Task SignIn(HttpClient client, string identityId) =>
        (
            await client.SendAsync(As(identityId, HttpMethod.Get, "/api/me"))
        ).EnsureSuccessStatusCode();

    /// <summary>An Automation for a Member to be refused a change to (#310, AC 9).</summary>
    static async Task<Guid> CreateAutomation(HttpClient client, string admin, Guid projectId)
    {
        var response = await client.SendAsync(
            As(
                admin,
                HttpMethod.Post,
                $"/api/projects/{projectId}/automations",
                new
                {
                    triggerLabel = "ai:arrange",
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                }
            )
        );
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    static Task<HttpResponseMessage> Grant(
        HttpClient client,
        string admin,
        Guid projectId,
        string identityId,
        string role
    ) =>
        client.SendAsync(
            As(admin, HttpMethod.Put, $"/api/projects/{projectId}/roles/{identityId}", new { role })
        );

    [Fact]
    public async Task TheCreator_Should_AdministerWhatTheyCreated()
    {
        using var client = Client();

        // Design D8. Not power taken by race — power over the one thing they brought into being.
        // Without it, nobody outside the configured list could ever get started.
        var projectId = await CreateProject(client, Owner);

        var me = await client.SendAsync(As(Owner, HttpMethod.Get, "/api/me"));
        var principal = await me.Content.ReadFromJsonAsync<MeResponse>();

        principal!
            .Projects.ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                standing => standing.ProjectId.ShouldBe(projectId),
                standing => standing.Role.ShouldBe("Admin")
            );
    }

    [Fact]
    public async Task AMember_Should_ObserveButNotConfigure()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);
        await SignIn(client, Member);
        (await Grant(client, Owner, projectId, Member, "Member")).EnsureSuccessStatusCode();

        // Observing: ACT-002 may view. The list is a Member operation and answers.
        var reading = await client.SendAsync(
            As(Member, HttpMethod.Get, $"/api/projects/{projectId}/automations")
        );
        reading.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Configuring: ACT-002 may NOT create or edit Automations. Refused by the pipeline, before
        // the handler — which is why no Automation validation message comes back.
        var configuring = await client.SendAsync(
            As(
                Member,
                HttpMethod.Post,
                $"/api/projects/{projectId}/automations",
                new
                {
                    triggerLabel = "ai:refine",
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                }
            )
        );
        configuring.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The lifecycle (#310, AC 9): a Member may READ it, because UC-007 has them reading the board
        // and the board's columns ARE this list — it carries stage names an Admin chose and no
        // credential, so there is nothing here a reader of the backlog does not already see on the
        // Stories themselves.
        var readingLifecycle = await client.SendAsync(
            As(Member, HttpMethod.Get, $"/api/projects/{projectId}/lifecycle")
        );
        readingLifecycle.StatusCode.ShouldBe(HttpStatusCode.OK);

        // …and may not rearrange it. The board offers a Member no control that assigns, moves or clears
        // a claim, but that is what is worth showing and never what is allowed: the refusal comes from
        // the pipeline's [Requires(ManageAutomations)], before the handler, so it holds for a request
        // made with no UI at all. AC 9's second clause is exactly this line.
        var owned = await CreateAutomation(client, Owner, projectId);
        var rearranging = await client.SendAsync(
            As(
                Member,
                HttpMethod.Put,
                $"/api/projects/{projectId}/automations/{owned}",
                new
                {
                    triggerLabel = "ai:arrange",
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                    toStage = "ai:next",
                }
            )
        );
        rearranging.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Runtime settings (#244, BR-009): gated both ways — the READ carries credential names,
        // the project's billing identity, so it is refused exactly like the write.
        var readingRuntimes = await client.SendAsync(
            As(Member, HttpMethod.Get, $"/api/projects/{projectId}/runtimes")
        );
        readingRuntimes.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var configuringRuntimes = await client.SendAsync(
            As(
                Member,
                HttpMethod.Put,
                $"/api/projects/{projectId}/runtimes",
                new
                {
                    defaultRuntime = "OpenCode",
                    credentialNames = new Dictionary<string, string>(),
                }
            )
        );
        configuringRuntimes.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminOnOneProject_Should_BeNothingOnAnother()
    {
        using var client = Client();
        var mine = await CreateProject(client, Owner);
        var theirs = await CreateProject(client, Stranger);

        // The whole point of scoping. Before this slice the Owner held one global role and would
        // have configured both.
        var reaching = await client.SendAsync(
            As(
                Owner,
                HttpMethod.Post,
                $"/api/projects/{theirs}/automations",
                new
                {
                    triggerLabel = "ai:refine",
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                }
            )
        );

        reaching.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        _ = mine;
    }

    [Fact]
    public async Task ARefusal_Should_NotSayWhetherTheProjectExists()
    {
        using var client = Client();
        var real = await CreateProject(client, Owner);
        var imaginary = Guid.NewGuid();

        var onSomethingReal = await client.SendAsync(
            As(Stranger, HttpMethod.Get, $"/api/projects/{real}/automations")
        );
        var onNothing = await client.SendAsync(
            As(Stranger, HttpMethod.Get, $"/api/projects/{imaginary}/automations")
        );

        // Identical, deliberately (task 2.4). A 403 here and a 404 there would turn the refusal into
        // a way to enumerate every project in the deployment.
        onSomethingReal.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        onNothing.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Compared field by field rather than as whole bodies: ProblemDetails stamps a per-request
        // traceId, so raw equality is a test that fails for a reason with nothing to do with
        // disclosure. What must match is everything a caller could learn from.
        var aboutTheReal = await onSomethingReal.Content.ReadFromJsonAsync<Refusal>();
        var aboutNothing = await onNothing.Content.ReadFromJsonAsync<Refusal>();

        aboutNothing!.Detail.ShouldBe(aboutTheReal!.Detail);
        aboutNothing.Code.ShouldBe(aboutTheReal.Code);
        aboutNothing.Title.ShouldBe(aboutTheReal.Title);
    }

    [Fact]
    public async Task ABootstrapAdministrator_Should_NeedNoGrant()
    {
        using var client = Client(bootstrap: Stranger);
        var projectId = await CreateProject(client, Owner);

        // Design D4: named in configuration, holding Admin everywhere, with no row anywhere.
        var response = await client.SendAsync(
            As(
                Stranger,
                HttpMethod.Post,
                $"/api/projects/{projectId}/automations",
                new
                {
                    triggerLabel = "ai:refine",
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                }
            )
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TwoIdsInOneConfigurationValue_Should_BothHoldAdmin()
    {
        // The shape a repository variable can actually carry: one string, separated. An array-only
        // reader would mean the deployed habitat could never name an administrator at all.
        using var client = Client(bootstrap: $"{Stranger}, {Member}");
        var projectId = await CreateProject(client, Owner);

        foreach (var administrator in new[] { Stranger, Member })
        {
            var response = await client.SendAsync(
                As(administrator, HttpMethod.Get, $"/api/projects/{projectId}/roles")
            );
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task WithNobodyConfiguredAndNoRoles_Should_LeaveNobodyHoldingAdmin()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);

        // Removing the creator's grant is the only way to reach the state task 3.3 announces, and
        // the guard refuses precisely because it is unrecoverable — which is the assertion.
        var stripping = await client.SendAsync(
            As(Owner, HttpMethod.Delete, $"/api/projects/{projectId}/roles/{Owner}")
        );

        stripping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stripping.Content.ReadAsStringAsync()).ShouldContain("only administrator");

        // And nobody else holds anything, with no configured list to fall back on.
        var stranger = await client.SendAsync(
            As(Stranger, HttpMethod.Get, $"/api/projects/{projectId}/roles")
        );
        stranger.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheOnlyAdministrator_Should_NotBeDemotableEither()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);

        // Demoting is the same dead end as removing, reached by a different button.
        var demoting = await Grant(client, Owner, projectId, Owner, "Member");

        demoting.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnAdministratorNamedInConfiguration_Should_MakeTheLastRowRemovable()
    {
        using var client = Client(bootstrap: Stranger);
        var projectId = await CreateProject(client, Owner);

        // The guard is about the outcome, not the count: with a configured administrator this is not
        // the last of anything, and refusing would have meant claiming otherwise.
        var response = await client.SendAsync(
            As(Owner, HttpMethod.Delete, $"/api/projects/{projectId}/roles/{Owner}")
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GrantingToSomebodyWhoHasNeverSignedIn_Should_BeRefused()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);

        var response = await Grant(client, Owner, projectId, NeverSeen, "Member");

        // Design D6's limitation with a voice: a role attaches to an identity this deployment has
        // actually met, and a row keyed on a name nobody has seen would look granted and do nothing.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("has not signed in");
    }

    [Fact]
    public async Task ChangingARole_Should_ReuseTheRowRatherThanAddOne()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);
        await SignIn(client, Member);

        (await Grant(client, Owner, projectId, Member, "Member")).EnsureSuccessStatusCode();
        (await Grant(client, Owner, projectId, Member, "Admin")).EnsureSuccessStatusCode();

        var roles = await client.SendAsync(
            As(Owner, HttpMethod.Get, $"/api/projects/{projectId}/roles")
        );
        var view = await roles.Content.ReadFromJsonAsync<RolesResponse>();

        view!.Holders.Count(holder => holder.IdentityId == Member).ShouldBe(1);
        view.Holders.Single(holder => holder.IdentityId == Member).Role.ShouldBe("Admin");

        // Two bundles, from the server's enum — DEC-034, not a pair typed into a form.
        view.Bundles.ShouldBe(["Member", "Admin"], ignoreOrder: true);
    }

    [Fact]
    public async Task TheProjectsList_Should_ShowOnlyWhatTheCallerMaySee()
    {
        using var client = Client();
        var mine = await CreateProject(client, Owner);
        _ = await CreateProject(client, Stranger);

        var listed = await client.SendAsync(As(Owner, HttpMethod.Get, "/api/projects"));
        var view = await listed.Content.ReadFromJsonAsync<ProjectsResponse>();

        // What FiltersToCaller commits to (design D7). An unfiltered list would name every project
        // in the deployment to a caller every operation on them refuses — which contradicts the
        // refusals, worded as they are so as not to disclose that a project exists.
        view!.Projects.ShouldHaveSingleItem().Id.ShouldBe(mine);
    }

    [Fact]
    public async Task TheCandidateList_Should_OfferOnlyPeopleTheDeploymentHasMet()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);
        await SignIn(client, Member);

        var roles = await client.SendAsync(
            As(Owner, HttpMethod.Get, $"/api/projects/{projectId}/roles")
        );
        var view = await roles.Content.ReadFromJsonAsync<RolesResponse>();

        // The creator holds a role, so they are a holder and not a candidate; the person who has
        // signed in and holds nothing is the candidate. Nobody unseen appears at all.
        view!.Candidates.ShouldHaveSingleItem().IdentityId.ShouldBe(Member);
        view.Holders.ShouldHaveSingleItem().IdentityId.ShouldBe(Owner);
    }

    [Fact]
    public async Task AMember_Should_NotSeeWhoElseHoldsARole()
    {
        using var client = Client();
        var projectId = await CreateProject(client, Owner);
        await SignIn(client, Member);
        (await Grant(client, Owner, projectId, Member, "Member")).EnsureSuccessStatusCode();

        // The roster carries the candidate list, which is everybody this deployment has ever seen.
        var response = await client.SendAsync(
            As(Member, HttpMethod.Get, $"/api/projects/{projectId}/roles")
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A caller named by a header, standing in for the provider only. The claim it writes is the one
    /// Entra writes, so <c>SignedInCaller</c> reads it by its real path.
    /// </summary>
    sealed class HeaderCaller(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Header = "X-Test-Oid";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identityId = values.ToString();
            var identity = new ClaimsIdentity(
                [
                    new Claim(
                        "http://schemas.microsoft.com/identity/claims/objectidentifier",
                        identityId
                    ),
                    new Claim("name", $"Person {identityId[..8]}"),
                ],
                "Test"
            );

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")
                )
            );
        }
    }

    sealed record Refusal(string Title, string Detail, string Code);

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record ProjectsResponse(List<ProjectResponse> Projects, int ArchivedCount);

    sealed record MeResponse(string Id, string DisplayName, List<Standing> Projects);

    sealed record Standing(Guid ProjectId, string Name, string Role);

    sealed record RolesResponse(
        List<HolderResponse> Holders,
        List<CandidateResponse> Candidates,
        List<string> Bundles
    );

    sealed record HolderResponse(string IdentityId, string DisplayName, string Role);

    sealed record CandidateResponse(string IdentityId, string DisplayName);
}
