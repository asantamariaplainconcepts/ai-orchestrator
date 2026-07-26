using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunOutputLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutputLink",
                schema: "runs",
                table: "runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OutputLink", schema: "runs", table: "runs");
        }
    }
}
