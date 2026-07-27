using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrillSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReadyLabel",
                schema: "projects",
                table: "automations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "RubricPath",
                schema: "projects",
                table: "automations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyLabel",
                schema: "projects",
                table: "automations"
            );

            migrationBuilder.DropColumn(
                name: "RubricPath",
                schema: "projects",
                table: "automations"
            );
        }
    }
}
