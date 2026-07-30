using AiOrchestrator.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Persistence;

sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public const string Schema = "projects";

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Automation> Automations => Set<Automation>();

    public DbSet<ProjectRoleAssignment> ProjectRoles => Set<ProjectRoleAssignment>();

    public DbSet<Person> People => Set<Person>();

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
            automation.Property(entity => entity.OutputLabel).HasMaxLength(200);

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

        modelBuilder.Entity<ProjectRoleAssignment>(assignment =>
        {
            assignment.ToTable("project_roles");
            assignment.HasKey(entity => entity.Id);
            assignment.Property(entity => entity.IdentityId).HasMaxLength(200).IsRequired();

            // Names, not ordinals — the same reason Automation stores its enums as strings.
            assignment.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(20);

            // One role per person per project, enforced by the database rather than by the handler
            // reading first: two concurrent grants would otherwise leave a person holding both, and
            // "which row wins" is not a question a permission check should ever have to ask.
            assignment
                .HasIndex(entity => new { entity.ProjectId, entity.IdentityId })
                .IsUnique();
        });

        modelBuilder.Entity<Person>(person =>
        {
            person.ToTable("people");
            person.HasKey(entity => entity.Id);
            person.Property(entity => entity.IdentityId).HasMaxLength(200).IsRequired();
            person.Property(entity => entity.DisplayName).HasMaxLength(200).IsRequired();

            // The identity id is the real key; Id is the surrogate every entity here carries.
            person.HasIndex(entity => entity.IdentityId).IsUnique();
        });
    }
}
