using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Backlog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorLocalSetupCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalSetupCommand",
                schema: "backlog",
                table: "connectors",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalSetupCommand",
                schema: "backlog",
                table: "connectors"
            );
        }
    }
}
