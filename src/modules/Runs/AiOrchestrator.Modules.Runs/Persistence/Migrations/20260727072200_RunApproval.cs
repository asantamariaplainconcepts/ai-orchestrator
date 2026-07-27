using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                schema: "runs",
                table: "runs",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                schema: "runs",
                table: "runs",
                type: "character varying(65536)",
                maxLength: 65536,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovedAt", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "Plan", schema: "runs", table: "runs");
        }
    }
}
