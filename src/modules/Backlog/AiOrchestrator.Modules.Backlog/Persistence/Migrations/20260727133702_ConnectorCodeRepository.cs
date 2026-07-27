using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Backlog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorCodeRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeRepository",
                schema: "backlog",
                table: "connectors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeRepository",
                schema: "backlog",
                table: "connectors"
            );
        }
    }
}
