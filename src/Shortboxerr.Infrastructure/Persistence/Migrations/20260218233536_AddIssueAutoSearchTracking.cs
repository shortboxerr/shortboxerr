using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAutoSearchTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSearchError",
                table: "Issues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSearchedAt",
                table: "Issues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchAttempts",
                table: "Issues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSearchError",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "LastSearchedAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SearchAttempts",
                table: "Issues");
        }
    }
}
