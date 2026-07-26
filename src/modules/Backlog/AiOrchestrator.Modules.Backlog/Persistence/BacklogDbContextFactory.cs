using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiOrchestrator.Modules.Backlog.Persistence;

/// <summary>Design-time only: lets `dotnet ef migrations` build the model without booting a host.</summary>
sealed class BacklogDbContextFactory : IDesignTimeDbContextFactory<BacklogDbContext>
{
    public BacklogDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<BacklogDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=aiorchestrator;Username=postgres;Password=postgres",
                    npgsql =>
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            BacklogDbContext.Schema
                        )
                )
                .Options
        );
}
