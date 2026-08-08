using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <summary>
    /// #296 — the Locus value `Pod` becomes `Sandbox`. The substrate it was named after no longer
    /// exists, and the domain glossary always said "never pod".
    /// <para>
    /// **The UPDATE is the whole migration; the AlterColumn is the footnote.** EF sees only the
    /// column default moving, because a renamed enum member looks like nothing to a model diff —
    /// but the value is persisted as a string, so every existing row still reads `Pod` and the
    /// next `Enum.Parse` on it throws. Scaffolding this and shipping it unedited would have left
    /// a database that loads no historical Run.
    /// </para>
    /// </summary>
    public partial class RunLocusIsASandbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE runs.runs SET "Locus" = 'Sandbox' WHERE "Locus" = 'Pod';"""
            );

            migrationBuilder.AlterColumn<string>(
                name: "Locus",
                schema: "runs",
                table: "runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sandbox",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pod"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE runs.runs SET "Locus" = 'Pod' WHERE "Locus" = 'Sandbox';"""
            );

            migrationBuilder.AlterColumn<string>(
                name: "Locus",
                schema: "runs",
                table: "runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pod",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Sandbox"
            );
        }
    }
}
