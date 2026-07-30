using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Projects.Features.Identity;

/// <summary>
/// Says out loud that nobody can administer anything (#13, task 3.3).
/// <para>
/// With no bootstrap administrators configured and no roles stored, every configuring operation in
/// the product is refused — correctly, and invisibly. That is the honest consequence of removing
/// the interim "everyone who signs in is Admin", and it gets a voice for the same reason #119's
/// "this deployment authenticates nobody" has one: an operator must be told, not left to discover
/// it by finding every button refused.
/// </para>
/// <para>
/// Registered only where callers sign in. On a machine one person owns the sentence would be false
/// — the owner administers everything — so the check never runs there.
/// </para>
/// </summary>
sealed partial class AdministrationAnnouncement(
    IServiceScopeFactory scopes,
    BootstrapAdministrators administrators,
    ILogger<AdministrationAnnouncement> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (administrators.IdentityIds.Count > 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();

        // A granted role is enough: somebody can administer their project, and they can grant more.
        // Only the empty-and-unconfigured case is the dead end worth announcing.
        if (await database.ProjectRoles.AnyAsync(cancellationToken))
        {
            return;
        }

        NobodyCanAdminister(logger, BootstrapAdministrators.ConfigurationKey);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Nobody can administer any project in this deployment: no roles are stored and "
            + "{ConfigurationKey} names no one. Signing in works and reading works, but every "
            + "operation that configures anything will be refused until an administrator is named "
            + "there — a comma-separated list of provider object ids"
    )]
    static partial void NobodyCanAdminister(ILogger logger, string configurationKey);
}
