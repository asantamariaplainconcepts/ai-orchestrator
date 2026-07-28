using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunLogChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "run_log_chunks",
                schema: "runs",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(
                        type: "character varying(8192)",
                        maxLength: 8192,
                        nullable: false
                    ),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_log_chunks", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_run_log_chunks_RunId_Sequence",
                schema: "runs",
                table: "run_log_chunks",
                columns: new[] { "RunId", "Sequence" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "run_log_chunks", schema: "runs");
        }
    }
}
