using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Series_Monitored",
                table: "Series",
                column: "Monitored");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Monitored_Status",
                table: "Issues",
                columns: new[] { "Monitored", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Status",
                table: "Issues",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Status_StoreDate",
                table: "Issues",
                columns: new[] { "Status", "StoreDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Monitored",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Issues_Monitored_Status",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_Status",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_Status_StoreDate",
                table: "Issues");
        }
    }
}
