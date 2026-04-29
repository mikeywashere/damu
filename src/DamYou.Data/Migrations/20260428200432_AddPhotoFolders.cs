using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhotoFolderId",
                table: "Photos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderPath = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Photos_PhotoFolderId",
                table: "Photos",
                column: "PhotoFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_FolderPath",
                table: "PhotoFolders",
                column: "FolderPath",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_PhotoFolders_PhotoFolderId",
                table: "Photos",
                column: "PhotoFolderId",
                principalTable: "PhotoFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_PhotoFolders_PhotoFolderId",
                table: "Photos");

            migrationBuilder.DropTable(
                name: "PhotoFolders");

            migrationBuilder.DropIndex(
                name: "IX_Photos_PhotoFolderId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "PhotoFolderId",
                table: "Photos");
        }
    }
}
