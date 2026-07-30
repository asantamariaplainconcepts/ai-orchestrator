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
        services.AddScoped<OverlapGuard>();

        // Who may do what, and where (#13). The rows live in this module's schema because a role is
        // a fact about a person's relationship to a Project, so the module that owns Projects owns
        // it — and the seam is in BuildingBlocks so the authorization decorator can read it without
        // any module referencing another.
        //
        // Composed per habitat, exactly like the principal it sits beside, and from the same
        // question: where nobody signs in there is one caller and the machine is theirs, so there is
        // nothing to look up. Deciding this here rather than inside the implementation is what keeps
        // "nobody is signed in yet" from being mistaken for "this person owns the place".
        if (BuildingBlocks.Identity.IdentityHabitat.CallersSignIn(configuration))
        {
            services.AddSingleton(Features.Identity.BootstrapAdministrators.From(configuration));
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
