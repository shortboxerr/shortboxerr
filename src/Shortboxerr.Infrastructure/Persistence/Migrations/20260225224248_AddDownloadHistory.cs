using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DownloadHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DownloadId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSite = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DestinationPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RetryAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    AverageSpeedBytesPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadHistories_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadHistories_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_CompletedAt",
                table: "DownloadHistories",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_DownloadId",
                table: "DownloadHistories",
                column: "DownloadId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_IssueId",
                table: "DownloadHistories",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_SeriesId",
                table: "DownloadHistories",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_SourceType",
                table: "DownloadHistories",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistories_State",
                table: "DownloadHistories",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadHistories");
        }
    }
}
