using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComicVineMetadataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "Series",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComicVineId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComicVineLastUpdated",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComicVinePublisherId",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineUrl",
                table: "Series",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Series",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataLastRefreshed",
                table: "Series",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalIssueCount",
                table: "Series",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComicVineId",
                table: "Issues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComicVineUrl",
                table: "Issues",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverDate",
                table: "Issues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Issues",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueNumberText",
                table: "Issues",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataLastRefreshed",
                table: "Issues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoreDate",
                table: "Issues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_ComicVineId",
                table: "Series",
                column: "ComicVineId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ComicVineId",
                table: "Issues",
                column: "ComicVineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_ComicVineId",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ComicVineId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVineLastUpdated",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVinePublisherId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVineUrl",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MetadataLastRefreshed",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "TotalIssueCount",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ComicVineId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ComicVineUrl",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "CoverDate",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IssueNumberText",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "MetadataLastRefreshed",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "StoreDate",
                table: "Issues");
        }
    }
}
