using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunTargetsAChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VendorStoryId",
                schema: "runs",
                table: "runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "AutomationId",
                schema: "runs",
                table: "runs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<string>(
                name: "Instruction",
                schema: "runs",
                table: "runs",
                type: "character varying(65536)",
                maxLength: 65536,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "RuntimeName",
                schema: "runs",
                table: "runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "TargetChangeBranch",
                schema: "runs",
                table: "runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "TargetChangeNumber",
                schema: "runs",
                table: "runs",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "TargetChangeTitle",
                schema: "runs",
                table: "runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "TargetChangeUrl",
                schema: "runs",
                table: "runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_runs_ProjectId_TargetChangeNumber",
                schema: "runs",
                table: "runs",
                columns: new[] { "ProjectId", "TargetChangeNumber" },
                unique: true,
                filter: "\"State\" IN ('Queued', 'Planning', 'AwaitingApproval', 'Executing', 'AwaitingInput')"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ProjectId_TargetChangeNumber",
                schema: "runs",
                table: "runs"
            );

            migrationBuilder.DropColumn(name: "Instruction", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "RuntimeName", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "TargetChangeBranch", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "TargetChangeNumber", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "TargetChangeTitle", schema: "runs", table: "runs");

            migrationBuilder.DropColumn(name: "TargetChangeUrl", schema: "runs", table: "runs");

            migrationBuilder.AlterColumn<string>(
                name: "VendorStoryId",
                schema: "runs",
                table: "runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "AutomationId",
                schema: "runs",
                table: "runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );
        }
    }
}
