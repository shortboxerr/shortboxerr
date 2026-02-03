using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPullListFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonitoringMode",
                table: "Series",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Issues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonitoringMode",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Issues");
        }
    }
}
