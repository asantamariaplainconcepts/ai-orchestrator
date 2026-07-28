using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // The third state (design D3): hosted, no provider, nobody authenticated. Real,
        // temporary, and invisible until identity existed as a concept. A condition with no
        // voice is how a stopgap becomes permanent.
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
/// Nobody signed in, because there is nothing to sign in to yet (OPN-002). Deliberately still a
/// principal: consumers never branch on identity being absent, and when a provider lands this
/// implementation is the one that disappears.
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
