using AiOrchestrator.Modules.Runs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Persistence;

sealed class RunsDbContext(DbContextOptions<RunsDbContext> options) : DbContext(options)
{
    public const string Schema = "runs";

    public DbSet<Run> Runs => Set<Run>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Run>(run =>
        {
            run.ToTable("runs");
            run.HasKey(entity => entity.Id);
            run.Property(entity => entity.VendorStoryId).HasMaxLength(200).IsRequired();

            // Names, not ordinals — the same self-describing-column rule Projects adopted
            // after #7's "0" projection.
            run.Property(entity => entity.State).HasConversion<string>().HasMaxLength(50);
            run.Property(entity => entity.FailureReason).HasMaxLength(1000);
            run.Property(entity => entity.OutputLink).HasMaxLength(500);
            run.Property(entity => entity.Plan).HasMaxLength(65536);

            // BR-001 as a constraint, not a hope: one Run per Story reference across the
            // active states. Every current state is active (see RunState); the filter is
            // written out so the day a terminal state exists, this index already excludes it
            // only if someone consciously edits the list — the rule stays chosen, not drifted.
            run.HasIndex(entity => new { entity.ProjectId, entity.VendorStoryId })
                .IsUnique()
                .HasFilter(RunStates.ActiveStateFilter());

            // BR-002 counts Planning/Executing per project; make that lookup cheap.
            run.HasIndex(entity => new { entity.ProjectId, entity.State });
        });
    }
}
