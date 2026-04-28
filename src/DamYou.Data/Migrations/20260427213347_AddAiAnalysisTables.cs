using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysisTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoColorPalettes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoColorPalettes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoColorPalettes_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoDetectedObjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxX = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxY = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxWidth = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxHeight = table.Column<float>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoDetectedObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoDetectedObjects_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    Dimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoEmbeddings_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoOcrTexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FullText = table.Column<string>(type: "TEXT", nullable: false),
                    TextEmbedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoOcrTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoOcrTexts_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoColorPalette_PhotoId",
                table: "PhotoColorPalettes",
                column: "PhotoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDetectedObjects_Label",
                table: "PhotoDetectedObjects",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoDetectedObjects_PhotoId",
                table: "PhotoDetectedObjects",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoEmbeddings_PhotoId",
                table: "PhotoEmbeddings",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoEmbeddings_PhotoId_ModelName",
                table: "PhotoEmbeddings",
                columns: new[] { "PhotoId", "ModelName" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoOcrText_PhotoId",
                table: "PhotoOcrTexts",
                column: "PhotoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoColorPalettes");

            migrationBuilder.DropTable(
                name: "PhotoDetectedObjects");

            migrationBuilder.DropTable(
                name: "PhotoEmbeddings");

            migrationBuilder.DropTable(
                name: "PhotoOcrTexts");
        }
    }
}
