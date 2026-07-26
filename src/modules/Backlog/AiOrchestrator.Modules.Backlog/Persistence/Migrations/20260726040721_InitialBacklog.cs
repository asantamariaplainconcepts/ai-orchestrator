using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Backlog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBacklog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "backlog");

            migrationBuilder.CreateTable(
                name: "connectors",
                schema: "backlog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vendor = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Repository = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    SecretName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    LastSyncedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastFailure = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    LastFailureAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connectors", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "stories",
                schema: "backlog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Title = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                    State = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Labels = table.Column<List<string>>(type: "text[]", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_connectors_ProjectId",
                schema: "backlog",
                table: "connectors",
                column: "ProjectId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_stories_ProjectId_VendorId",
                schema: "backlog",
                table: "stories",
                columns: new[] { "ProjectId", "VendorId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "connectors", schema: "backlog");

            migrationBuilder.DropTable(name: "stories", schema: "backlog");
        }
    }
}
