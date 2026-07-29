using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAutomationTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL because this is an expression index and EF cannot express one (#147, design
            // D1). BR-003 was a handler convention: the only index here was non-unique, so two
            // concurrent saves of one trigger both passed the in-memory check and both inserted.
            //
            // THE NULL TRAP, handled deliberately. TriggerState is nullable and Postgres treats
            // NULLs as *distinct* in a unique index — so an index over the raw column would happily
            // hold two rows with the same label and no state, which is precisely the duplicate this
            // prevents. COALESCE gives an absent state a value, and states the intent inside the
            // index rather than relying on NULLS NOT DISTINCT, which needs Postgres 15.
            //
            // lower() on both, because the vendor compares label names case-insensitively; the
            // domain uses OrdinalIgnoreCase and these two must agree.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_automations_trigger_identity"
                ON projects.automations ("ProjectId", lower("TriggerLabel"), COALESCE(lower("TriggerState"), ''));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX projects."IX_automations_trigger_identity";""");
        }
    }
}
