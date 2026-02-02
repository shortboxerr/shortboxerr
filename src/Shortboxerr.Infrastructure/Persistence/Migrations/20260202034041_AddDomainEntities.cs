using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SortTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalSource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EditionTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SortTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    EditionType = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Isbn = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalSource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasFile = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditionTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditionTitles_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueNumber = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalSource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasFile = table.Column<bool>(type: "INTEGER", nullable: false),
                    SatisfiedByEdition = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EditionContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EditionTitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssueNumber = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditionContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditionContents_EditionTitles_EditionTitleId",
                        column: x => x.EditionTitleId,
                        principalTable: "EditionTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EditionContents_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EditionContents_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FileAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Format = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    EditionTitleId = table.Column<int>(type: "INTEGER", nullable: true),
                    QualityScore = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileAssets_EditionTitles_EditionTitleId",
                        column: x => x.EditionTitleId,
                        principalTable: "EditionTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FileAssets_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    EditionTitleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Data = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    SourcePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    DestinationPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_EditionTitles_EditionTitleId",
                        column: x => x.EditionTitleId,
                        principalTable: "EditionTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditionContents_EditionTitleId_SortOrder",
                table: "EditionContents",
                columns: new[] { "EditionTitleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EditionContents_IssueId",
                table: "EditionContents",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_EditionContents_SeriesId",
                table: "EditionContents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_EditionTitles_Isbn",
                table: "EditionTitles",
                column: "Isbn");

            migrationBuilder.CreateIndex(
                name: "IX_EditionTitles_SeriesId",
                table: "EditionTitles",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_EditionTitles_Title",
                table: "EditionTitles",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_EditionTitleId",
                table: "FileAssets",
                column: "EditionTitleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Hash",
                table: "FileAssets",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_IssueId",
                table: "FileAssets",
                column: "IssueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Path",
                table: "FileAssets",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_EditionTitleId",
                table: "HistoryEvents",
                column: "EditionTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_EventType",
                table: "HistoryEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_IssueId",
                table: "HistoryEvents",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_SeriesId",
                table: "HistoryEvents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_Timestamp",
                table: "HistoryEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SeriesId_IssueNumber",
                table: "Issues",
                columns: new[] { "SeriesId", "IssueNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_ExternalSource_ExternalId",
                table: "Series",
                columns: new[] { "ExternalSource", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_Title",
                table: "Series",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditionContents");

            migrationBuilder.DropTable(
                name: "FileAssets");

            migrationBuilder.DropTable(
                name: "HistoryEvents");

            migrationBuilder.DropTable(
                name: "EditionTitles");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
