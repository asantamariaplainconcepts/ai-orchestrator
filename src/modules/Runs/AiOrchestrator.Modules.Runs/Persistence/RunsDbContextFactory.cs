using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiOrchestrator.Modules.Runs.Persistence;

/// <summary>Design-time only: lets `dotnet ef migrations` build the model without booting a host.</summary>
sealed class RunsDbContextFactory : IDesignTimeDbContextFactory<RunsDbContext>
{
    public RunsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<RunsDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=aiorchestrator;Username=postgres;Password=postgres",
                    npgsql =>
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RunsDbContext.Schema)
                )
                .Options
        );
}
