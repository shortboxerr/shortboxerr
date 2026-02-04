using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesStatusSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusSource",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusSource",
                table: "Series");
        }
    }
}
