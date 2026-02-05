using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCachedDiscoveryWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedDiscoveryWeeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeekStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastRefreshed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheTier = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedDiscoveryWeeks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedDiscoveryWeeks_ExpiresAt",
                table: "CachedDiscoveryWeeks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CachedDiscoveryWeeks_WeekStart",
                table: "CachedDiscoveryWeeks",
                column: "WeekStart",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedDiscoveryWeeks");
        }
    }
}
