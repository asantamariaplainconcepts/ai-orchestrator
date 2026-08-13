using AiOrchestrator.Modules.Backlog.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Persistence;

sealed class BacklogDbContext(DbContextOptions<BacklogDbContext> options) : DbContext(options)
{
    public const string Schema = "backlog";

    public DbSet<Connector> Connectors => Set<Connector>();

    public DbSet<Story> Stories => Set<Story>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Connector>(connector =>
        {
            connector.ToTable("connectors");
            connector.Property(entity => entity.WebhookSecretName).HasMaxLength(200);
            connector.Property(entity => entity.CodeRepository).HasMaxLength(200);
            connector.Property(entity => entity.PromptDirectory).HasMaxLength(200);
            connector.HasKey(entity => entity.Id);
            // One Connector per Project — enforced by the database, not only by the handler.
            connector.HasIndex(entity => entity.ProjectId).IsUnique();
            connector.Property(entity => entity.Owner).HasMaxLength(200).IsRequired();
            connector.Property(entity => entity.Repository).HasMaxLength(200).IsRequired();
            // No longer required: a Connector on the host path stores no secret name at all, and a
            // name that resolved to nothing would be worse than an absent one (DEC-069). What makes
            // the two states distinguishable is AuthenticatesAsHost below, not this column's
            // emptiness.
            connector.Property(entity => entity.SecretName).HasMaxLength(200);
            // False for every Connector written before this change, which is exactly what they are:
            // the database default is what makes that true of existing rows rather than hoped for.
            connector.Property(entity => entity.AuthenticatesAsHost).HasDefaultValue(false);
            connector.Property(entity => entity.LastFailure).HasMaxLength(1000);
            // The database default is what makes every pre-#210 row read as Repository —
            // an int column defaulting to 0 would read as no enum value at all.
            connector
                .Property(entity => entity.CodeSource)
                .HasDefaultValue(CodeSource.Repository);
            connector.Property(entity => entity.LocalPath).HasMaxLength(500);
            // Nullable with no default: null is "no setup command", which is what every Connector
            // written before #332 means and a valid configuration in its own right.
            connector.Property(entity => entity.LocalSetupCommand).HasMaxLength(500);
        });

        modelBuilder.Entity<Story>(story =>
        {
            story.ToTable("stories");
            // Generous but bounded: GitHub caps issue bodies at 65536 characters.
            story.Property(entity => entity.Body).HasMaxLength(65536);
            story.HasKey(entity => entity.Id);
            // Identity is (project, vendor id) — a rename must not create a second Story.
            story.HasIndex(entity => new { entity.ProjectId, entity.VendorId }).IsUnique();
            story.Property(entity => entity.VendorId).HasMaxLength(100).IsRequired();
            story.Property(entity => entity.Title).HasMaxLength(1000).IsRequired();
            story.Property(entity => entity.State).HasMaxLength(50).IsRequired();
            story.Property(entity => entity.Labels).HasColumnType("text[]");
        });
    }
}
