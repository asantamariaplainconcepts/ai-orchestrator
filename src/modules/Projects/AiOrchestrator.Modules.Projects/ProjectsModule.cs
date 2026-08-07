using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Projects.Features.Automations;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Projects;

public sealed class ProjectsModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    public override string Name => "Projects";

    public override async Task Migrate(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        // ACT-002 may see what will act on this Project's Stories and nothing more: an Automation
        // decides when an agent touches a repository, so creating or editing one is configuration,
        // and so is retiring the Project or changing who may administer it.
        services.AddPermissionGrants(
            BuildingBlocks.Identity.ProjectRole.Member,
            ProjectPermissions.ReadAutomations
        );
        services.AddDbContext<ProjectsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString(ConnectionStringName),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        ProjectsDbContext.Schema
                    );
                    // Transient connection failures (container restarts, pool churn) retry
                    // instead of surfacing as a 500 — first seen as an intermittent E2E red.
                    npgsql.EnableRetryOnFailure();
                }
            )
        );

        // Health must mean "can serve requests", which for this module includes its database.
        // The self-only check let the host report healthy before the DB was usable.
        services.AddHealthChecks().AddDbContextCheck<ProjectsDbContext>("projects-db");

        // The Contracts read surface — the owner registers its own implementation.
        services.AddScoped<IAutomationCatalog, AutomationCatalog>();

        // What other modules may ask about a Project's standing (#121): Backlog decides whether
        // to poll, Runs decides whether to start work. Scoped and asked per decision — never a
        // cached copy, which would keep polling a Project an Admin just retired.
        services.AddScoped<Contracts.IProjectCatalog, Features.Projects.ProjectCatalog>();
        services.AddScoped<
            Contracts.IProjectRuntimeSettings,
            Features.Projects.ProjectRuntimeSettings
        >();
        services.AddScoped<OverlapGuard>();
        services.AddScoped<PipelineDiscovery>();
        services.AddScoped<StarterInstaller>();

        // Who may do what, and where (#13). The rows live in this module's schema because a role is
        // a fact about a person's relationship to a Project, so the module that owns Projects owns
        // it — and the seam is in BuildingBlocks so the authorization decorator can read it without
        // any module referencing another.
        //
        // Composed per habitat, exactly like the principal it sits beside, and from the same
        // question: where nobody signs in there is one caller and the machine is theirs, so there is
        // nothing to look up. Deciding this here rather than inside the implementation is what keeps
        // "nobody is signed in yet" from being mistaken for "this person owns the place".
        // Registered in every habitat, unlike the reader below: the last-administrator guard has to
        // know whether anybody holds Admin without a row, and a guard that could not ask would have
        // to refuse the safe case and say something untrue about why.
        services.AddSingleton(Features.Identity.BootstrapAdministrators.From(configuration));

        // One writer for "this deployment has met this person", used by both paths that create the
        // obligation: signing in, and creating a Project (which grants its creator Admin).
        services.AddScoped<Features.Identity.KnownPeople>();

        if (BuildingBlocks.Identity.IdentityHabitat.CallersSignIn(configuration))
        {
            services.AddScoped<
                BuildingBlocks.Identity.IProjectPermissions,
                Features.Identity.StoredProjectRoles
            >();
            services.AddHostedService<Features.Identity.AdministrationAnnouncement>();
        }
        else
        {
            services.AddSingleton<
                BuildingBlocks.Identity.IProjectPermissions,
                Features.Identity.SoleOccupantPermissions
            >();
        }
    }
}
