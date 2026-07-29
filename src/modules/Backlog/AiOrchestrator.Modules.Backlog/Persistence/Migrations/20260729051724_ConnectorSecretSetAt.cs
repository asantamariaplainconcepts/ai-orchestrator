using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Backlog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorSecretSetAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SecretSetAt",
                schema: "backlog",
                table: "connectors",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretSetAt",
                schema: "backlog",
                table: "connectors"
            );
        }
    }
}
