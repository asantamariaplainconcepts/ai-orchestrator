using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "runs");

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorStoryId = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    DispatchedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_runs_ProjectId_State",
                schema: "runs",
                table: "runs",
                columns: new[] { "ProjectId", "State" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_runs_ProjectId_VendorStoryId",
                schema: "runs",
                table: "runs",
                columns: new[] { "ProjectId", "VendorStoryId" },
                unique: true,
                filter: "\"State\" IN ('Queued', 'Planning', 'AwaitingApproval', 'Executing')"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "runs", schema: "runs");
        }
    }
}
