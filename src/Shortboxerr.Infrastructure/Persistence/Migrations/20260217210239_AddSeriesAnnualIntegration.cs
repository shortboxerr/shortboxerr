using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesAnnualIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentSeriesId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesType",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Series_ParentSeriesId",
                table: "Series",
                column: "ParentSeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Series_Series_ParentSeriesId",
                table: "Series",
                column: "ParentSeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Series_Series_ParentSeriesId",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_ParentSeriesId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ParentSeriesId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SeriesType",
                table: "Series");
        }
    }
}
