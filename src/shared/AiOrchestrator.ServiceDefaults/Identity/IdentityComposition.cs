using System.Security.Claims;
using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

namespace AiOrchestrator.ServiceDefaults.Identity;

/// <summary>
/// Composes who the caller is, per habitat (#119). On a machine somebody owns, they are the
/// owner; on the web they are whoever signed in — and until an identity provider exists, that
/// second case is a state this refuses to leave silent.
/// </summary>
public static class IdentityComposition
{
    /// <summary>Set to <c>LocalOwner</c> on a machine one person owns. Absent everywhere else.</summary>
    public const string ModeKey = "Identity:Mode";

    /// <summary>What the local owner is called in the portal and in attribution.</summary>
    public const string OwnerNameKey = "Identity:OwnerName";

    internal const string LocalOwnerMode = "LocalOwner";

    /// <summary>
    /// The provider's configuration section. Presence is what composes it (#12, design D2) — never
    /// an environment name, for the reason recorded in <see cref="UnsafeFor"/>: the self-host
    /// compose defaults to Production, and gating on that once refused to start the very habitat
    /// DEC-049 protects.
    /// </summary>
    public const string ProviderSectionKey = "AzureAd";

    /// <summary>Whether this host authenticates through the identity provider.</summary>
    public static bool UsesProvider(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[$"{ProviderSectionKey}:ClientId"]);

    public static TBuilder AddIdentity<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var local = string.Equals(
            builder.Configuration[ModeKey],
            LocalOwnerMode,
            StringComparison.OrdinalIgnoreCase
        );

        if (local)
        {
            // The second lock (design D2). The first is that Terraform never sets the value;
            // this one survives a person editing the portal. Refusing to start rather than
            // warning, because what is being prevented is serving an implicit Admin to the
            // internet, and a process that will not start is the only signal nobody misses.
            // Same shape as the worker that refuses to start without a database (#92).
            var reason = UnsafeFor(builder);
            if (reason is not null)
            {
                throw new InvalidOperationException(
                    $"{ModeKey}={LocalOwnerMode} is for a machine one person owns, and this "
                        + $"deployment is not one: {reason}. The local owner holds the Admin role "
                        + "with no sign-in, so serving it here would hand administration to "
                        + "anyone who can reach the address. Remove the setting."
                );
            }

            builder.Services.AddSingleton<ICurrentPrincipal>(
                new LocalOwner(builder.Configuration[OwnerNameKey])
            );
            return builder;
        }

        // The provider mode (#12, DEC-058): a confidential BFF client. The session is an HttpOnly
        // cookie; no token reaches the browser. Composed on configuration presence, and only in a
        // host that serves HTTP — the worker has no callers to authenticate.
        if (UsesProvider(builder.Configuration))
        {
            builder
                .Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection(ProviderSectionKey));

            // The public cloud unless told otherwise (#170). Microsoft.Identity.Web refuses to run
            // without an Instance, and it refuses PER REQUEST — the deployed portal answered 500 on
            // everything, /health included, because the handler initializes inside the auth
            // middleware. The two ids are the configuration a deployment actually varies; the
            // instance only changes for sovereign clouds, which can still set it.
            builder.Services.Configure<MicrosoftIdentityOptions>(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    if (string.IsNullOrWhiteSpace(options.Instance))
                    {
                        options.Instance = "https://login.microsoftonline.com/";
                    }
                }
            );

            builder.Services.Configure<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    // Lax, and Strict is the loop (#176): the provider's response is a
                    // cross-site form POST, and the redirect that follows it is a navigation
                    // initiated from that cross-site context — a Strict cookie is not attached
                    // to it, so the landing page challenges again and Entra silently signs the
                    // user straight back in, forever. Lax still withholds the cookie from
                    // cross-site subrequests and POSTs; it rides top-level navigations, which
                    // is exactly the post-login redirect. The handshake cookies stay at the
                    // library's defaults for the sibling reason (DEC-058, corrected by DEC-059).
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.HttpOnly = true;
                    // SameAsRequest rather than Always: the dev profile is plain http on
                    // localhost, and a Secure cookie over plain http is one that never comes
                    // back. Browsers treat localhost as trustworthy, deployed traffic is https,
                    // so this hardens exactly where hardening can work.
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }
            );

            // The fallback's RequireAuthorization needs the services even though no policy is
            // custom: the default policy is "authenticated", which is exactly the semantics.
            builder.Services.AddAuthorization();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ICurrentPrincipal, SignedInCaller>();
            return builder;
        }

        // The third state (design D3): hosted, no provider, nobody authenticated. Real,
        // temporary, and invisible until identity existed as a concept. A condition with no
        // voice is how a stopgap becomes permanent. It stays after the provider exists, because
        // the self-host habitat (DEC-049) has no tenant to sign into.
        builder.Services.AddSingleton<ICurrentPrincipal, UnauthenticatedCaller>();
        return builder;
    }

    /// <summary>Announced once at startup rather than per request, which would be noise.</summary>
    public static void WarnIfUnauthenticated(IServiceProvider services, ILogger logger)
    {
        if (services.GetRequiredService<ICurrentPrincipal>() is UnauthenticatedCaller)
        {
            IdentityLog.NobodyIsAuthenticated(logger);
        }
    }

    /// <summary>The reason this deployment is not somebody's machine, or null when it is.</summary>
    static string? UnsafeFor(IHostApplicationBuilder builder)
    {
        // A managed secret store is the one thing only the provisioned deployment has: Terraform
        // configures it, and neither `aspire run` nor the self-host compose can. Infrastructure
        // somebody provisioned is not a machine somebody owns.
        //
        // NOT the environment name, which was tried and is wrong: the self-host compose sets no
        // ASPNETCORE_ENVIRONMENT, so ASP.NET defaults it to Production, and gating on that
        // refused to start the very habitat DEC-049 protects. Found by booting it (#99's lesson
        // — a generator's green means "I wrote a file", never "the file works").
        if (
            !string.IsNullOrWhiteSpace(
                builder.Configuration[Secrets.SecretResolution.KeyVaultUriKey]
            )
        )
        {
            return "it is wired to a managed secret store, so it is provisioned infrastructure";
        }

        var addresses = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
        var reachable = addresses
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(address => Uri.TryCreate(address, UriKind.Absolute, out _))
            .Select(address => new Uri(address).Host)
            .FirstOrDefault(host => host is not ("localhost" or "127.0.0.1" or "[::1]"));

        // Deliberately not treated as unsafe: a wildcard bind. Every container binds every
        // interface — the self-host compose included — so using it as the signal would refuse
        // to start in exactly the habitat DEC-049 exists to protect. What distinguishes a
        // deployment from a machine is the environment it declares, not how it listens.
        return reachable is null ? null : $"it listens on {reachable}, which is not loopback";
    }
}

/// <summary>
/// The machine's owner (design D4): constructed from configuration, never stored. There is no
/// users table to seed and no row to migrate, because what this knows is simply that one person
/// has the machine.
/// </summary>
sealed class LocalOwner(string? name) : ICurrentPrincipal
{
    public Principal Current { get; } =
        new(
            "local-owner",
            string.IsNullOrWhiteSpace(name) ? "Local owner" : name,
            PrincipalRole.Admin
        );
}

/// <summary>
/// Whoever signed in (#12): the session's claims, read per request through the accessor. A
/// singleton over <see cref="IHttpContextAccessor"/> rather than a scoped service, so the startup
/// warning can still resolve the seam from the root scope.
/// <para>
/// Every signed-in user holds Admin — the interim rule the requirement states out loud. Role
/// assignment per project is #13, landing on this same seam. The unauthenticated window (the
/// challenge itself, health probes) reads as a Member-role anonymous, never Admin: API routes are
/// refused before a handler runs, so this value deciding anything would already be a bug.
/// </para>
/// </summary>
sealed class SignedInCaller(IHttpContextAccessor accessor) : ICurrentPrincipal
{
    public Principal Current
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new Principal("anonymous", "Not signed in", PrincipalRole.Member);
            }

            // The object id is the stable identity; a name claim is a label that may change.
            var id =
                user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                ?? user.FindFirstValue("oid")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "signed-in";

            var name =
                user.FindFirstValue("name")
                ?? user.FindFirstValue(ClaimTypes.Name)
                ?? user.Identity.Name
                ?? "Signed in";

            return new Principal(id, name, PrincipalRole.Admin);
        }
    }
}

/// <summary>
/// Nobody signed in, because this host has no provider configured. Deliberately still a
/// principal: consumers never branch on identity being absent. Kept after the provider exists,
/// because the self-host habitat (DEC-049) has no tenant — this is the row that warns at startup.
/// </summary>
sealed class UnauthenticatedCaller : ICurrentPrincipal
{
    public Principal Current { get; } = new("anonymous", "Not signed in", PrincipalRole.Admin);
}

static partial class IdentityLog
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "This deployment authenticates nobody: every caller is treated as an "
            + "administrator. Open decision OPN-002 tracks closing this; until it does, the "
            + "address must not be reachable by anyone you would not make an administrator"
    )]
    public static partial void NobodyIsAuthenticated(ILogger logger);
}
