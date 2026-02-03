using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditionCoverAndComicVineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComicVineId",
                table: "EditionTitles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineUrl",
                table: "EditionTitles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "EditionTitles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "EditionTitles");

            migrationBuilder.DropColumn(
                name: "ComicVineUrl",
                table: "EditionTitles");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "EditionTitles");
        }
    }
}
