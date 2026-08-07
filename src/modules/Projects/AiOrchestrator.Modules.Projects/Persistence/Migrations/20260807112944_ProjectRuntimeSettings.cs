using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultRuntime",
                schema: "projects",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "Runtime",
                schema: "projects",
                table: "automations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50
            );

            migrationBuilder.CreateTable(
                name: "project_runtime_credentials",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Runtime = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    SecretName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_runtime_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_runtime_credentials_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_project_runtime_credentials_ProjectId_Runtime",
                schema: "projects",
                table: "project_runtime_credentials",
                columns: new[] { "ProjectId", "Runtime" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "project_runtime_credentials", schema: "projects");

            migrationBuilder.DropColumn(
                name: "DefaultRuntime",
                schema: "projects",
                table: "projects"
            );

            migrationBuilder.AlterColumn<string>(
                name: "Runtime",
                schema: "projects",
                table: "automations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true
            );
        }
    }
}
