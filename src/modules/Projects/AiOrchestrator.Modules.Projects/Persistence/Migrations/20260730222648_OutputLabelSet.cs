using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <summary>
    /// One output label becomes a set (#165).
    /// <para>
    /// <b>Hand-written, and that is the point.</b> What EF scaffolded for this type change was
    /// <c>DropColumn</c> then <c>AddColumn</c> — it even warned that it "may result in the loss of
    /// data". Applied as generated, it would have silently discarded every hand-off configured in
    /// the deployment: every workflow edge, gone, with the schema perfectly correct afterwards.
    /// </para>
    /// <para>
    /// So the column is widened rather than replaced: add, copy across, drop. The reverse is lossy
    /// by nature — a set of three cannot become one label — and takes the first element, which is
    /// the only honest inverse of a widening and is written down rather than pretended away.
    /// </para>
    /// </summary>
    public partial class OutputLabelSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "OutputLabels",
                schema: "projects",
                table: "automations",
                type: "character varying(200)[]",
                nullable: false,
                defaultValue: new string[0]
            );

            // A configured label becomes a set of one; an unset one becomes the empty set, which is
            // what "ends silently" means now. NULLIF guards the blank strings a form could have
            // stored before the trim landed.
            migrationBuilder.Sql(
                """
                UPDATE projects.automations
                SET "OutputLabels" = ARRAY["OutputLabel"]
                WHERE NULLIF(TRIM("OutputLabel"), '') IS NOT NULL;
                """
            );

            migrationBuilder.DropColumn(
                name: "OutputLabel",
                schema: "projects",
                table: "automations"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutputLabel",
                schema: "projects",
                table: "automations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            // Lossy, and deliberately not disguised: an Automation that handed on to three places
            // keeps the first and forgets the rest, because the old shape cannot hold them.
            migrationBuilder.Sql(
                """
                UPDATE projects.automations
                SET "OutputLabel" = "OutputLabels"[1]
                WHERE array_length("OutputLabels", 1) >= 1;
                """
            );

            migrationBuilder.DropColumn(
                name: "OutputLabels",
                schema: "projects",
                table: "automations"
            );
        }
    }
}
