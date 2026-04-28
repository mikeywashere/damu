using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DamYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePhotoProcessingStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename column and convert data from bool (0/1) to enum (0=Unprocessed, 2=Processed)
            migrationBuilder.RenameColumn(
                name: "IsProcessed",
                table: "Photos",
                newName: "Status");

            // Update existing data: false (0) stays as 0 (Unprocessed), true (1) becomes 2 (Processed)
            migrationBuilder.Sql("UPDATE Photos SET Status = CASE WHEN Status = 1 THEN 2 ELSE 0 END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert data back: Processed (2) to true (1), Unprocessed (0) to false (0)
            migrationBuilder.Sql("UPDATE Photos SET Status = CASE WHEN Status = 2 THEN 1 ELSE 0 END");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Photos",
                newName: "IsProcessed");
        }
    }
}
