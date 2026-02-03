using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueMetadataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnnual",
                table: "Issues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpecial",
                table: "Issues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpecialType",
                table: "Issues",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IssueStoryArcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComicVineStoryArcId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ComicVineUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueStoryArcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueStoryArcs_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SeriesId_IsAnnual",
                table: "Issues",
                columns: new[] { "SeriesId", "IsAnnual" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueStoryArcs_ComicVineStoryArcId",
                table: "IssueStoryArcs",
                column: "ComicVineStoryArcId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueStoryArcs_IssueId",
                table: "IssueStoryArcs",
                column: "IssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueStoryArcs");

            migrationBuilder.DropIndex(
                name: "IX_Issues_SeriesId_IsAnnual",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IsAnnual",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IsSpecial",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SpecialType",
                table: "Issues");
        }
    }
}
