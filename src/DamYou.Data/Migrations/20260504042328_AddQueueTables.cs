using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QueuedFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueuedFolders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FolderPath = table.Column<string>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedFiles_FilePath",
                table: "QueuedFiles",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueuedFiles_Status",
                table: "QueuedFiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedFolders_FolderPath",
                table: "QueuedFolders",
                column: "FolderPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueuedFolders_Status",
                table: "QueuedFolders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueuedFiles");

            migrationBuilder.DropTable(
                name: "QueuedFolders");
        }
    }
}
