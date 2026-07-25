using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiOrchestrator.Modules.Projects.Persistence;

/// <summary>
/// Design-time only: lets `dotnet ef migrations` build the model without booting the host.
/// The connection string here is never used at runtime.
/// </summary>
sealed class ProjectsDbContextFactory : IDesignTimeDbContextFactory<ProjectsDbContext>
{
    public ProjectsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ProjectsDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=aiorchestrator;Username=postgres;Password=postgres",
                    npgsql =>
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            ProjectsDbContext.Schema
                        )
                )
                .Options
        );
}
