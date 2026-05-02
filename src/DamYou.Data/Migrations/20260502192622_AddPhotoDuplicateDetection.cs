using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoDuplicateDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoDuplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    DateDiscovered = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoDuplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoDuplicates_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDuplicates_FilePath",
                table: "PhotoDuplicates",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDuplicates_PhotoId",
                table: "PhotoDuplicates",
                column: "PhotoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoDuplicates");
        }
    }
}
