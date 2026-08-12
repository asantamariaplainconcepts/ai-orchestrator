using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAutomationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                schema: "projects",
                table: "automations"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                schema: "projects",
                table: "automations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
