using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationPreviewPort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviewPort",
                schema: "projects",
                table: "automations",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewPort",
                schema: "projects",
                table: "automations"
            );
        }
    }
}
