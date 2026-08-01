using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Backlog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorCodeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeSource",
                schema: "backlog",
                table: "connectors",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
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
            migrationBuilder.DropColumn(name: "CodeSource", schema: "backlog", table: "connectors");

            migrationBuilder.DropColumn(name: "LocalPath", schema: "backlog", table: "connectors");
        }
    }
}
