using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunExecutionLocus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                schema: "runs",
                table: "runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Locus",
                schema: "runs",
                table: "runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pod"
            );

            migrationBuilder.AddColumn<string>(
                name: "WorkingFolder",
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
            migrationBuilder.DropColumn(name: "BranchName", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "Locus", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "WorkingFolder", schema: "runs", table: "runs");
        }
    }
}
