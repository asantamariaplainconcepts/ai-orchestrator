using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostUsd",
                schema: "runs",
                table: "runs",
                type: "numeric",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAt",
                schema: "runs",
                table: "runs",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "runs",
                table: "runs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "runs",
                table: "runs",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "UsageInputTokens",
                schema: "runs",
                table: "runs",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "UsageOutputTokens",
                schema: "runs",
                table: "runs",
                type: "bigint",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CostUsd", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "EndedAt", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "FailureReason", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "StartedAt", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "UsageInputTokens", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "UsageOutputTokens", schema: "runs", table: "runs");
        }
    }
}
