using AiOrchestrator.Modules.Runs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Persistence;

sealed class RunsDbContext(DbContextOptions<RunsDbContext> options) : DbContext(options)
{
    public const string Schema = "runs";

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunLogChunk> LogChunks => Set<RunLogChunk>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // A conversation is not a Run (#166) and its tables say so: no story-uniqueness index, no
        // state the cap counts, nothing that could make one occupy a slot by accident.
        modelBuilder.Entity<Conversation>(conversation =>
        {
            conversation.ToTable("conversations");
            conversation.HasKey(entity => entity.Id);
            conversation.Property(entity => entity.VendorStoryId).HasMaxLength(200);

            // Owned in the aggregate sense: messages are loaded and saved with the conversation and
            // are never queried on their own, so the navigation is the only way in.
            conversation
                .HasMany(entity => entity.Messages)
                .WithOne()
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            conversation.Navigation(entity => entity.Messages).AutoInclude();

            conversation.HasIndex(entity => new { entity.ProjectId, entity.LastActivityAt });
        });

        modelBuilder.Entity<ConversationMessage>(message =>
        {
            message.ToTable("conversation_messages");
            message.HasKey(entity => entity.Id);
            message.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(20);
            message.Property(entity => entity.Body).HasMaxLength(65536).IsRequired();

            // Explicit precision: a cost read back as a rounded double is a cost that stops adding
            // up, and BR-011 already distinguishes unknown from zero.
            message.Property(entity => entity.CostUsd).HasPrecision(18, 6);
        });

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

            modelBuilder.Entity<RunLogChunk>(chunk =>
            {
                chunk.ToTable("run_log_chunks");
                chunk.HasKey(entity => entity.Id);
                // Reads are always "the whole log for one Run, in order".
                chunk.HasIndex(entity => new { entity.RunId, entity.Sequence });
                chunk.Property(entity => entity.Content).HasMaxLength(8192);
            });
        });
    }
}
