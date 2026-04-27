using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "Photos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PipelineTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineTasks_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTasks_CreatedAt",
                table: "PipelineTasks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTasks_PhotoId",
                table: "PipelineTasks",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTasks_Status",
                table: "PipelineTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineTasks");

            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "Photos");
        }
    }
}
