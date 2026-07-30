using AiOrchestrator.ServiceDefaults.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace AiOrchestrator.Server;

/// <summary>
/// The pipeline half of the provider mode (#12): middleware, the surface split and the three
/// auth endpoints. The service half lives in <see cref="IdentityComposition"/>; both key on the
/// same configuration presence, so the two cannot disagree about which mode this host is in.
/// </summary>
static class SignInPipeline
{
    /// <summary>Wires sign-in when the provider is configured. Returns whether it did.</summary>
    public static bool UseSignIn(this WebApplication app)
    {
        if (!IdentityComposition.UsesProvider(app.Configuration))
        {
            return false;
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // The surface split (design D4): an API call gets an answer, never an ambush. A fetch
        // that receives a 302 toward the provider dies as an opaque CORS failure, so /api
        // answers 401 with a problem body and the SPA can say "sign in again".
        //
        // Two deliberate carve-outs, each authenticated by something that is not a cookie:
        //  - /api/webhooks: the vendor signs those deliveries (BR-015), and GitHub cannot hold
        //    a session. A cookie gate here would break ingest the day sign-in ships.
        //  - /hubs is NOT exempt: it sits outside /api, and leaving it open would stream every
        //    Run's log to anyone who found the address. Same-origin cookies ride the SignalR
        //    handshake, so the session works there unchanged.
        app.Use(
            async (context, next) =>
            {
                var path = context.Request.Path;
                var guarded =
                    (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/webhooks"))
                    || path.StartsWithSegments("/hubs");

                if (guarded && context.User.Identity?.IsAuthenticated != true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    // The content type rides the write call: setting the header first is
                    // overwritten by WriteAsJsonAsync's own default of application/json.
                    await context.Response.WriteAsJsonAsync(
                        new
                        {
                            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                            title = "Not signed in.",
                            status = 401,
                            detail = "Your session has ended. Sign in again to continue.",
                        },
                        options: null,
                        contentType: "application/problem+json"
                    );
                    return;
                }

                await next();
            }
        );

        // Sign-in is a navigation by construction — the challenge is a redirect.
        app.MapGet(
            "/auth/signin",
            () =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [OpenIdConnectDefaults.AuthenticationScheme]
                )
        );

        // Both sessions end (design D5): the cookie here, the provider's through the
        // front-channel logout entra-app.sh registered. Ending only the cookie signs the user
        // straight back in on the next challenge, which reads as "sign out does nothing".
        app.MapGet(
            "/auth/signout",
            () =>
                Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/signed-out" },
                    [
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        OpenIdConnectDefaults.AuthenticationScheme,
                    ]
                )
        );

        // Served outside the SPA on purpose: the SPA's fallback requires a session, and a
        // signed-out page that immediately re-challenged would make signing out impossible to
        // observe. A person who signed out chose to — sign-in is offered, never forced.
        app.MapGet(
            "/signed-out",
            () =>
                Results.Content(
                    "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
                        + "<title>Signed out</title></head><body style=\"font-family:system-ui;"
                        + "display:grid;place-items:center;min-height:100dvh;margin:0\">"
                        + "<main style=\"text-align:center\"><h1>Signed out</h1>"
                        + "<p>Your session has ended.</p>"
                        + "<p><a href=\"/auth/signin\">Sign in again</a></p></main></body></html>",
                    "text/html"
                )
        );

        return true;
    }
}
