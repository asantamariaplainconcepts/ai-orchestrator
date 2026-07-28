using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchivedProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "projects",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ArchivedAt", schema: "projects", table: "projects");
        }
    }
}
