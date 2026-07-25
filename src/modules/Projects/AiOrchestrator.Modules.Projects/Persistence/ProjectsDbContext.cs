using AiOrchestrator.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Persistence;

sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public const string Schema = "projects";

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Project>(project =>
        {
            project.ToTable("projects");
            project.HasKey(entity => entity.Id);
            project.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
            project.HasIndex(entity => entity.Name).IsUnique();
        });
    }
}
