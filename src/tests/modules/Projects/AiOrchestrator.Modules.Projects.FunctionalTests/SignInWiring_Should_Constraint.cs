using System.Net;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #12 — the provider mode's wiring, asserted with no live tenant (design D6). The tenant was
/// exercised for real once, by DEC-058; what CI must keep proving is the composition: 401 for
/// APIs, challenge for navigations, and the two carve-outs that something other than a cookie
/// authenticates. The provider's metadata is supplied statically so no request leaves the host.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class SignInWiring_Should_Constraint(ProjectsApiFixture fixture)
{
    HttpClient Client() =>
        fixture
            .WithWebHostBuilder(builder =>
            {
                // Exactly what the deployment carries and nothing more (#170): the first version
                // of this test also set AzureAd:Instance, which made it green over a configuration
                // more complete than the deployed one — and the gap it papered over answered 500 on
                // every request in dev, health probes included. The instance must come from the
                // code's public-cloud default.
                builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000001");
                builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000002");
                builder.ConfigureTestServices(services =>
                    services.PostConfigure<OpenIdConnectOptions>(
                        OpenIdConnectDefaults.AuthenticationScheme,
                        options =>
                            // Static metadata, so a challenge composes its redirect without the
                            // handler fetching the discovery document — CI has no business
                            // calling the real authority to prove our own pipeline order. The
                            // MANAGER is what must be replaced: the handler consults it, and the
                            // one built at configure time already points at the network —
                            // setting options.Configuration alone still fetched, and the 500 its
                            // IOException caused is how this line earned its comment.
                            options.ConfigurationManager =
                                new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                    new OpenIdConnectConfiguration
                                    {
                                        Issuer = "https://login.microsoftonline.com/test/v2.0",
                                        AuthorizationEndpoint =
                                            "https://login.microsoftonline.com/test/oauth2/v2.0/authorize",
                                        TokenEndpoint =
                                            "https://login.microsoftonline.com/test/oauth2/v2.0/token",
                                        EndSessionEndpoint =
                                            "https://login.microsoftonline.com/test/oauth2/v2.0/logout",
                                    }
                                )
                    )
                );
            })
            .CreateClient(
                new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                }
            );

    [Fact]
    public async Task AnApiCallWithoutASession_Should_Get401AndNeverARedirect()
    {
        using var client = Client();

        var response = await client.GetAsync("/api/me");

        // An answer, not an ambush (design D4): a fetch that received a 302 toward the provider
        // would die as an opaque CORS failure.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Sign in again");
    }

    [Fact]
    public async Task ANavigationWithoutASession_Should_BeChallengedToTheProvider()
    {
        using var client = Client();

        var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsoluteUri.ShouldStartWith(
            "https://login.microsoftonline.com/"
        );
    }

    [Fact]
    public async Task TheChallenge_Should_BuildItsRedirectFromTheForwardedScheme()
    {
        using var client = Client();

        // What the TLS-terminating ingress actually sends (#174): the request arrives over http
        // with X-Forwarded-Proto=https. Without forwarded-header processing the challenge asked
        // Entra for an http redirect the registration cannot carry (AADSTS50011).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.AbsoluteUri;
        location.ShouldContain("redirect_uri=https%3A%2F%2F");
    }

    [Fact]
    public async Task TheSignedOutPage_Should_NeedNoSession()
    {
        using var client = Client();

        // A signed-out page that re-challenged would make signing out impossible to observe.
        var response = await client.GetAsync("/signed-out");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("/auth/signin");
    }

    [Fact]
    public async Task AWebhookDelivery_Should_ReachItsOwnSignatureCheck()
    {
        using var client = Client();

        // The vendor signs deliveries (BR-015) and cannot hold a session, so the cookie gate
        // must not intercept this path. Observed, not assumed: with no connector matching the
        // delivery the endpoint answers 200 — which is exactly the proof needed here, because a
        // gated request could only ever be 401 with the session problem body. Reaching ANY
        // endpoint verdict means the carve-out held.
        var response = await client.PostAsync(
            "/api/webhooks/github",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        );

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Sign in again");
    }

    [Fact]
    public async Task TheDeploysSmokeCheck_Should_NeedNoSession()
    {
        using var client = Client();

        // deploy.yml verifies a release by polling /api/health until 200, and a machine polling
        // liveness can never hold a session. Gating it is what failed deploy run #39 at its
        // verify step — the release itself had succeeded (#172).
        var response = await client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithNoProviderConfigured_Should_BehaveExactlyAsBefore()
    {
        // The fixture's own client: no AzureAd configuration, the stopgap row. This is the mode
        // every other functional test runs in, and it must not have changed.
        var response = await fixture.CreateClient().GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
