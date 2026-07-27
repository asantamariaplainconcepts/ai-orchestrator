using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AwaitingInputState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ProjectId_VendorStoryId",
                schema: "runs",
                table: "runs"
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WaitingSince",
                schema: "runs",
                table: "runs",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_runs_ProjectId_VendorStoryId",
                schema: "runs",
                table: "runs",
                columns: new[] { "ProjectId", "VendorStoryId" },
                unique: true,
                filter: "\"State\" IN ('Queued', 'Planning', 'AwaitingApproval', 'Executing', 'AwaitingInput')"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ProjectId_VendorStoryId",
                schema: "runs",
                table: "runs"
            );

            migrationBuilder.DropColumn(name: "WaitingSince", schema: "runs", table: "runs");

            migrationBuilder.CreateIndex(
                name: "IX_runs_ProjectId_VendorStoryId",
                schema: "runs",
                table: "runs",
                columns: new[] { "ProjectId", "VendorStoryId" },
                unique: true,
                filter: "\"State\" IN ('Queued', 'Planning', 'AwaitingApproval', 'Executing')"
            );
        }
    }
}
