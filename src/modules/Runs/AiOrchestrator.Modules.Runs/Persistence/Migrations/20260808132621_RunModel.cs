using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model",
                schema: "runs",
                table: "runs",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ResolvedModel",
                schema: "runs",
                table: "runs",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Model", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "ResolvedModel", schema: "runs", table: "runs");
        }
    }
}
