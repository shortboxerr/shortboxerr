using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VariantCovers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComicVineImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ImageTags = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    VariantType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsPrimaryCover = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPreferred = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantCovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantCovers_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VariantCovers_ComicVineImageId",
                table: "VariantCovers",
                column: "ComicVineImageId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantCovers_IssueId",
                table: "VariantCovers",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantCovers_IssueId_IsPreferred",
                table: "VariantCovers",
                columns: new[] { "IssueId", "IsPreferred" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VariantCovers");
        }
    }
}
