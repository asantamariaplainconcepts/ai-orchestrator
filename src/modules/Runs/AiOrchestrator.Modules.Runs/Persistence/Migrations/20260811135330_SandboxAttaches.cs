using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SandboxAttaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sandbox_attaches",
                schema: "runs",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Sandbox = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Who = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sandbox_attaches", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_sandbox_attaches_At",
                schema: "runs",
                table: "sandbox_attaches",
                column: "At"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sandbox_attaches_Sandbox_At",
                schema: "runs",
                table: "sandbox_attaches",
                columns: new[] { "Sandbox", "At" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "sandbox_attaches", schema: "runs");
        }
    }
}
