using AiOrchestrator.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Persistence;

sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public const string Schema = "projects";

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Automation> Automations => Set<Automation>();

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

        modelBuilder.Entity<Automation>(automation =>
        {
            automation.ToTable("automations");
            automation.HasKey(entity => entity.Id);
            automation.Property(entity => entity.TriggerLabel).HasMaxLength(200).IsRequired();
            automation.Property(entity => entity.TriggerState).HasMaxLength(100);
            automation.Property(entity => entity.RubricPath).HasMaxLength(300);
            automation.Property(entity => entity.ReadyLabel).HasMaxLength(200);

            // Names, not ordinals. #7 shipped a projection where an enum read back as "0" because
            // EF translated ToString() to SQL; storing the name makes the column self-describing
            // and removes the whole class of mistake.
            automation
                .Property(entity => entity.Action)
                .HasConversion<string>()
                .HasMaxLength(50);
            automation.Property(entity => entity.Runtime).HasConversion<string>().HasMaxLength(50);

            // Not a unique index on (ProjectId, TriggerLabel, TriggerState): it would catch exact
            // duplicates and silently miss the subsumption case, which is the interesting one
            // (design D4). The rule lives in the handler; this index only makes the lookup cheap.
            automation.HasIndex(entity => entity.ProjectId);
        });
    }
}
